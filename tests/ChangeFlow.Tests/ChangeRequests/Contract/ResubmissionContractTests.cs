using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using ChangeFlow.Tests.ChangeRequests.Infrastructure;
using static ChangeFlow.Tests.ChangeRequests.Contract.ReviewWorkflowContractTests;

namespace ChangeFlow.Tests.ChangeRequests.Contract;

public sealed class ResubmissionContractTests(ChangeRequestApiFixture fixture)
    : IClassFixture<ChangeRequestApiFixture>
{
    [Fact]
    public async Task Corrected_request_can_be_resubmitted_and_approved_with_both_decisions_preserved()
    {
        using var owner = fixture.CreateAuthenticatedClient(fixture.Requester);
        using var reviewer = fixture.CreateAuthenticatedClient(fixture.Reviewer);
        var path = await CreatePendingAsync(owner);
        await ReviseAndApproveAsync(owner, reviewer, path, fixture.Requester.Value, fixture.SecondReviewer.Value);
        var approved = await owner.GetFromJsonAsync<JsonElement>(path);
        var historyPath = $"{path}/review-history";
        var history = await owner.GetFromJsonAsync<JsonElement>(historyPath);
        Assert.Equal(new[] { "RequestChanges", "Approve" },
            history.EnumerateArray().Select(item => item.GetProperty("action").GetString()));
        Assert.Equal("Revise scope", history[0].GetProperty("feedback").GetString());
        await AssertApprovedCommandsRejectedAsync(owner, reviewer, path);
        Assert.Equal(approved.GetRawText(), (await owner.GetFromJsonAsync<JsonElement>(path)).GetRawText());
        Assert.Equal(history.GetRawText(), (await owner.GetFromJsonAsync<JsonElement>(historyPath)).GetRawText());
    }

    internal static async Task ReviseAndApproveAsync(
        HttpClient owner, HttpClient reviewer, string path, Guid ownerId, Guid attemptedOwner)
    {
        using var requested = await reviewer.PostAsJsonAsync($"{path}/request-changes",
            new { expectedVersion = "2", feedback = "Revise scope" });
        Assert.Equal(HttpStatusCode.OK, requested.StatusCode);
        var before = await owner.GetFromJsonAsync<JsonElement>(path);
        Assert.Equal("Revise scope", before.GetProperty("currentFeedback").GetString());
        using var edit = await owner.PutAsJsonAsync(path, new
        {
            title = "Revised", description = "New detail", reason = "Updated reason", expectedVersion = "3",
            requesterId = attemptedOwner, currentFeedback = "Attempted replacement"
        });
        Assert.Equal(HttpStatusCode.OK, edit.StatusCode);
        var edited = await edit.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("ChangesRequested", edited.GetProperty("status").GetString());
        Assert.Equal("4", edited.GetProperty("version").GetString());
        Assert.Equal(ownerId, edited.GetProperty("requesterId").GetGuid());
        Assert.Equal("Revise scope", edited.GetProperty("currentFeedback").GetString());
        Assert.Equal(edited.GetRawText(), (await owner.GetFromJsonAsync<JsonElement>(path)).GetRawText());

        using var submit = await owner.PostAsJsonAsync($"{path}/submit", new { expectedVersion = "4" });
        Assert.Equal(HttpStatusCode.OK, submit.StatusCode);
        var resubmitted = await submit.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("PendingReview", resubmitted.GetProperty("status").GetString());
        Assert.Equal("5", resubmitted.GetProperty("version").GetString());
        Assert.False(resubmitted.TryGetProperty("currentFeedback", out _));
        Assert.Equal(resubmitted.GetRawText(), (await reviewer.GetFromJsonAsync<JsonElement>(path)).GetRawText());
        using var approve = await reviewer.PostAsJsonAsync($"{path}/approve", new { expectedVersion = "5" });
        Assert.Equal(HttpStatusCode.OK, approve.StatusCode);
        var approved = await approve.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Approved", approved.GetProperty("status").GetString());
        Assert.Equal("6", approved.GetProperty("version").GetString());
        Assert.Equal("Revised", approved.GetProperty("title").GetString());
    }

    internal static async Task AssertApprovedCommandsRejectedAsync(HttpClient owner, HttpClient reviewer, string path)
    {
        await AssertProblemAsync(await owner.PutAsJsonAsync(path,
            new { title = "Rejected", description = "Rejected", reason = "Rejected", expectedVersion = "6" }),
            HttpStatusCode.Conflict, "invalid_state");
        await AssertProblemAsync(await owner.PostAsJsonAsync($"{path}/submit", new { expectedVersion = "6" }),
            HttpStatusCode.Conflict, "invalid_state");
        await AssertProblemAsync(await reviewer.PostAsJsonAsync($"{path}/approve", new { expectedVersion = "6" }),
            HttpStatusCode.Conflict, "invalid_state");
        await AssertProblemAsync(await reviewer.PostAsJsonAsync($"{path}/request-changes",
            new { expectedVersion = "6", feedback = "Rejected" }), HttpStatusCode.Conflict, "invalid_state");
    }
}
