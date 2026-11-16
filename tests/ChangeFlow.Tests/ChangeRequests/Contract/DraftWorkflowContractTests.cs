using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using ChangeFlow.Core.Domain.ChangeRequests;
using ChangeFlow.Infra.DataAccess.Common;
using ChangeFlow.Tests.ChangeRequests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ChangeFlow.Tests.ChangeRequests.Contract;

public sealed class DraftWorkflowContractTests(ChangeRequestApiFixture fixture)
    : IClassFixture<ChangeRequestApiFixture>
{
    [Fact]
    public async Task Identical_requests_by_the_same_owner_are_persisted_independently()
    {
        using var client = fixture.CreateAuthenticatedClient(fixture.Requester);
        var input = new { title = "Same title", description = "Same detail", reason = "Same reason" };
        using var first = await client.PostAsJsonAsync("/change-requests", input);
        using var second = await client.PostAsJsonAsync("/change-requests", input);
        Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        Assert.Equal(HttpStatusCode.Created, second.StatusCode);
        var firstBody = await first.Content.ReadFromJsonAsync<JsonElement>();
        var secondBody = await second.Content.ReadFromJsonAsync<JsonElement>();
        var firstId = firstBody.GetProperty("id").GetGuid();
        var secondId = secondBody.GetProperty("id").GetGuid();
        Assert.NotEqual(firstId, secondId);
        Assert.Equal("1", firstBody.GetProperty("version").GetString());
        Assert.Equal("1", secondBody.GetProperty("version").GetString());

        var factory = fixture.Factory.Services.GetRequiredService<IDbContextFactory<ChangeFlowDbContext>>();
        await using var context = await factory.CreateDbContextAsync();
        var rows = await context.Set<ChangeRequest>()
            .Where(request => request.Id == firstId || request.Id == secondId).ToListAsync();
        Assert.Equal(2, rows.Count);
        Assert.All(rows, row => Assert.Equal(
            (fixture.Requester, ChangeRequestStatus.Draft, 1L, input.title, input.description, input.reason),
            (row.RequesterId, row.Status, row.Version, row.Title, row.Description, row.Reason)));
    }
    [Fact]
    public async Task Owner_can_create_read_edit_and_submit_using_the_current_version()
    {
        using var client = fixture.CreateAuthenticatedClient(fixture.Requester);
        using var response = await client.PostAsJsonAsync("/change-requests",
            new { title = " Title ", description = " Description ", reason = " Reason ",
                requesterId = fixture.Reviewer.Value, status = "Approved" });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var created = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(fixture.Requester.Value, created.GetProperty("requesterId").GetGuid());
        Assert.Equal("Title", created.GetProperty("title").GetString());
        Assert.Equal("Description", created.GetProperty("description").GetString());
        Assert.Equal("Reason", created.GetProperty("reason").GetString());
        Assert.Equal("Draft", created.GetProperty("status").GetString());
        Assert.Equal("1", created.GetProperty("version").GetString());
        Assert.False(created.TryGetProperty("currentFeedback", out _));
        Assert.Equal($"/change-requests/{created.GetProperty("id").GetGuid()}",
            response.Headers.Location?.OriginalString);

        var location = response.Headers.Location!;
        var read = await client.GetFromJsonAsync<JsonElement>(location);
        Assert.Equal(created.GetRawText(), read.GetRawText());

        using var edit = await client.PutAsJsonAsync(location,
            new { title = " Revised ", description = " Detail ", reason = " Need ", expectedVersion = "1" });
        Assert.Equal(HttpStatusCode.OK, edit.StatusCode);
        var edited = await edit.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Revised", edited.GetProperty("title").GetString());
        Assert.Equal("2", edited.GetProperty("version").GetString());
        using var submit = await client.PostAsJsonAsync($"{location}/submit", new { expectedVersion = "2" });
        Assert.Equal(HttpStatusCode.OK, submit.StatusCode);
        var submitted = await submit.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("PendingReview", submitted.GetProperty("status").GetString());
        Assert.Equal("3", submitted.GetProperty("version").GetString());
        await AssertProblem(await client.PostAsJsonAsync($"{location}/submit", new { expectedVersion = "3" }),
            HttpStatusCode.Conflict, "invalid_state");
        Assert.Equal(submitted.GetRawText(),
            (await client.GetFromJsonAsync<JsonElement>(location)).GetRawText());
    }

    [Fact]
    public async Task Rejected_changes_leave_the_persisted_draft_unchanged()
    {
        using var owner = fixture.CreateAuthenticatedClient(fixture.Requester);
        using var otherRequester = fixture.CreateAuthenticatedClient(fixture.DualRoleUser);
        using var reviewer = fixture.CreateAuthenticatedClient(fixture.Reviewer);
        using var anonymous = fixture.Factory.CreateClient();
        using var created = await owner.PostAsJsonAsync("/change-requests",
            new { title = "Original", description = "Detail", reason = "Need" });
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var location = created.Headers.Location!;
        var original = await created.Content.ReadFromJsonAsync<JsonElement>();

        await AssertProblem(await anonymous.GetAsync(location), HttpStatusCode.Unauthorized, "authentication_required");
        await AssertProblem(await otherRequester.GetAsync(location), HttpStatusCode.Forbidden, "forbidden");
        await AssertProblem(await owner.GetAsync("/change-requests/not-a-uuid"), HttpStatusCode.BadRequest, "validation_failed");
        await AssertProblem(await owner.GetAsync($"/change-requests/{Guid.NewGuid()}"),
            HttpStatusCode.NotFound, "change_request_not_found");
        await AssertProblem(await reviewer.PostAsJsonAsync("/change-requests",
            new { title = "Other", description = "Detail", reason = "Need" }),
            HttpStatusCode.Forbidden, "forbidden");
        await AssertProblem(await otherRequester.PutAsJsonAsync(location,
            new { title = "Other", description = "Detail", reason = "Need", expectedVersion = "1" }),
            HttpStatusCode.Forbidden, "forbidden");
        await AssertProblem(await reviewer.PostAsJsonAsync($"{location}/submit", new { expectedVersion = "1" }),
            HttpStatusCode.Forbidden, "forbidden");
        await AssertProblem(await owner.PostAsJsonAsync($"{location}/submit", new { }),
            HttpStatusCode.BadRequest, "validation_failed");
        await AssertProblem(await owner.PostAsJsonAsync($"{location}/submit",
            new { expectedVersion = "9223372036854775808" }), HttpStatusCode.BadRequest, "validation_failed");
        await AssertProblem(await owner.PostAsJsonAsync($"{location}/submit",
            new { expectedVersion = "1\0" }), HttpStatusCode.BadRequest, "validation_failed");
        await AssertProblem(await owner.PostAsJsonAsync($"{location}/submit", new { expectedVersion = "2" }),
            HttpStatusCode.Conflict, "version_conflict");
        await AssertProblem(await owner.PutAsJsonAsync(location,
            new { title = " ", description = "Detail", reason = "Need", expectedVersion = "1" }),
            HttpStatusCode.BadRequest, "validation_failed");
        using var malformed = new StringContent("{", Encoding.UTF8, "application/json");
        await AssertProblem(await owner.PutAsync(location, malformed),
            HttpStatusCode.BadRequest, "validation_failed");

        Assert.Equal(original.GetRawText(),
            (await owner.GetFromJsonAsync<JsonElement>(location)).GetRawText());
    }

    private static async Task AssertProblem(HttpResponseMessage response, HttpStatusCode status, string code)
    {
        using (response)
        {
            Assert.Equal(status, response.StatusCode);
            Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
            var problem = await response.Content.ReadFromJsonAsync<JsonElement>();
            Assert.Equal(code, problem.GetProperty("code").GetString());
            Assert.True(problem.TryGetProperty("traceId", out _));
        }
    }
}
