using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using ChangeFlow.Tests.ChangeRequests.Infrastructure;
using ChangeFlow.Infra.DataAccess.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ChangeFlow.Tests.ChangeRequests.Contract;

public sealed class ReviewWorkflowContractTests(ChangeRequestApiFixture fixture)
    : IClassFixture<ChangeRequestApiFixture>
{
    [Fact]
    public async Task Owner_and_participating_current_reviewer_can_read_history()
    {
        using var owner = fixture.CreateAuthenticatedClient(fixture.Requester);
        using var reviewer = fixture.CreateAuthenticatedClient(fixture.Reviewer);
        using var other = fixture.CreateAuthenticatedClient(fixture.SecondReviewer);
        var path = await CreatePendingAsync(owner);
        var historyPath = $"{path}/review-history";
        Assert.Empty((await owner.GetFromJsonAsync<JsonElement>(historyPath)).EnumerateArray());
        var pending = await reviewer.GetFromJsonAsync<JsonElement>(path);
        Assert.Equal("PendingReview", pending.GetProperty("status").GetString());
        await AssertProblemAsync(await other.GetAsync(historyPath), HttpStatusCode.Forbidden, "forbidden");

        await AssertProblemAsync(await reviewer.PostAsJsonAsync($"{path}/request-changes",
            new { expectedVersion = "2", feedback = " " }), HttpStatusCode.BadRequest, "validation_failed");
        using var decision = await reviewer.PostAsJsonAsync($"{path}/request-changes",
            new { expectedVersion = "2", feedback = " Revise the scope " });
        Assert.Equal(HttpStatusCode.OK, decision.StatusCode);
        var changed = await decision.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("ChangesRequested", changed.GetProperty("status").GetString());
        Assert.Equal("3", changed.GetProperty("version").GetString());
        Assert.Equal("Revise the scope", changed.GetProperty("currentFeedback").GetString());
        Assert.Equal(changed.GetRawText(), (await owner.GetFromJsonAsync<JsonElement>(path)).GetRawText());
        await AssertProblemAsync(await reviewer.GetAsync(path), HttpStatusCode.Forbidden, "forbidden");

        var anotherPath = await CreatePendingAsync(owner);
        using var approval = await other.PostAsJsonAsync($"{anotherPath}/approve", new { expectedVersion = "2" });
        Assert.Equal(HttpStatusCode.OK, approval.StatusCode);
        var approved = await approval.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Approved", approved.GetProperty("status").GetString());
        Assert.False(approved.TryGetProperty("currentFeedback", out _));
        var approvalHistory = await owner.GetFromJsonAsync<JsonElement>($"{anotherPath}/review-history");
        Assert.Equal(JsonValueKind.Null, Assert.Single(approvalHistory.EnumerateArray()).GetProperty("feedback").ValueKind);
        await AssertProblemAsync(await other.GetAsync(historyPath), HttpStatusCode.Forbidden, "forbidden");

        var history = await owner.GetFromJsonAsync<JsonElement>(historyPath);
        var item = Assert.Single(history.EnumerateArray());
        Assert.Equal(fixture.Reviewer.Value, item.GetProperty("reviewerId").GetGuid());
        Assert.Equal("RequestChanges", item.GetProperty("action").GetString());
        Assert.Equal("Revise the scope", item.GetProperty("feedback").GetString());
        Assert.Equal(fixture.Clock.GetUtcNow(), item.GetProperty("decidedAtUtc").GetDateTimeOffset());
        Assert.Equal(history.GetRawText(), (await reviewer.GetFromJsonAsync<JsonElement>(historyPath)).GetRawText());

        var factory = fixture.Factory.Services.GetRequiredService<IDbContextFactory<ChangeFlowDbContext>>();
        await using var context = await factory.CreateDbContextAsync();
        var revoked = fixture.Reviewer.Value;
        try
        {
            Assert.Equal(1, await context.Database.ExecuteSqlAsync(
                $"DELETE FROM user_roles WHERE user_id = {revoked} AND role = 'Reviewer'"));
            await AssertProblemAsync(await reviewer.GetAsync(historyPath), HttpStatusCode.Forbidden, "forbidden");
            Assert.Equal(history.GetRawText(), (await owner.GetFromJsonAsync<JsonElement>(historyPath)).GetRawText());
        }
        finally
        {
            await context.Database.ExecuteSqlAsync(
                $"INSERT OR IGNORE INTO user_roles (user_id, role) VALUES ({revoked}, 'Reviewer')");
        }
    }

    [Fact]
    public async Task History_returns_decisions_in_resulting_version_order()
    {
        using var owner = fixture.CreateAuthenticatedClient(fixture.Requester);
        using var reviewer = fixture.CreateAuthenticatedClient(fixture.Reviewer);
        using var second = fixture.CreateAuthenticatedClient(fixture.SecondReviewer);
        var path = await CreatePendingAsync(owner);
        using var requestChanges = await reviewer.PostAsJsonAsync($"{path}/request-changes",
            new { expectedVersion = "2", feedback = "Revise" });
        Assert.Equal(HttpStatusCode.OK, requestChanges.StatusCode);

        // Prepare a second review round directly. The resubmission endpoint is verified in US3.
        var id = Guid.Parse(path[(path.LastIndexOf('/') + 1)..]);
        var factory = fixture.Factory.Services.GetRequiredService<IDbContextFactory<ChangeFlowDbContext>>();
        await using var context = await factory.CreateDbContextAsync();
        Assert.Equal(1, await context.Database.ExecuteSqlAsync(
            $"UPDATE change_requests SET status = 'PendingReview', version = 4 WHERE id = {id} AND version = 3"));
        using var approve = await second.PostAsJsonAsync($"{path}/approve", new { expectedVersion = "4" });
        Assert.Equal(HttpStatusCode.OK, approve.StatusCode);
        var history = await owner.GetFromJsonAsync<JsonElement>($"{path}/review-history");
        Assert.Equal(new[] { "RequestChanges", "Approve" },
            history.EnumerateArray().Select(item => item.GetProperty("action").GetString()));
        Assert.Equal(new[] { fixture.Reviewer.Value, fixture.SecondReviewer.Value },
            history.EnumerateArray().Select(item => item.GetProperty("reviewerId").GetGuid()));
    }

    [Theory]
    [InlineData("approve")]
    [InlineData("request-changes")]
    public async Task Review_rejections_preserve_request_and_history(string command)
    {
        using var dual = fixture.CreateAuthenticatedClient(fixture.DualRoleUser);
        using var reviewer = fixture.CreateAuthenticatedClient(fixture.Reviewer);
        var path = await CreatePendingAsync(dual);

        await AssertProblemAsync(await dual.PostAsJsonAsync($"{path}/{command}",
            new { expectedVersion = "2", feedback = "Revise" }),
            HttpStatusCode.Forbidden, "self_review_forbidden");
        using var approved = await reviewer.PostAsJsonAsync($"{path}/approve", new { expectedVersion = "2" });
        Assert.Equal(HttpStatusCode.OK, approved.StatusCode);
        var before = await dual.GetFromJsonAsync<JsonElement>(path);
        var historyPath = $"{path}/review-history";
        var history = await dual.GetFromJsonAsync<JsonElement>(historyPath);
        Assert.Single(history.EnumerateArray());

        foreach (var (version, code) in new[] { ("3", "invalid_state"), ("2", "version_conflict") })
        {
            await AssertProblemAsync(await reviewer.PostAsJsonAsync($"{path}/{command}",
                new { expectedVersion = version, feedback = "Revise" }), HttpStatusCode.Conflict, code);
            Assert.Equal(before.GetRawText(), (await dual.GetFromJsonAsync<JsonElement>(path)).GetRawText());
            Assert.Equal(history.GetRawText(), (await dual.GetFromJsonAsync<JsonElement>(historyPath)).GetRawText());
        }
    }

    internal static async Task<string> CreatePendingAsync(HttpClient owner)
    {
        using var create = await owner.PostAsJsonAsync("/change-requests",
            new { title = "Title", description = "Detail", reason = "Need" });
        Assert.Equal(HttpStatusCode.Created, create.StatusCode);
        var path = create.Headers.Location!.OriginalString;
        using var submit = await owner.PostAsJsonAsync($"{path}/submit", new { expectedVersion = "1" });
        Assert.Equal(HttpStatusCode.OK, submit.StatusCode);
        return path;
    }

    internal static async Task AssertProblemAsync(HttpResponseMessage response, HttpStatusCode status, string code)
    {
        using (response)
        {
            Assert.Equal(status, response.StatusCode);
            Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
            var problem = await response.Content.ReadFromJsonAsync<JsonElement>();
            Assert.Equal(code, problem.GetProperty("code").GetString());
        }
    }
}
