using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using ChangeFlow.Core.Domain.ChangeRequests;
using ChangeFlow.Infra.DataAccess.Common;
using ChangeFlow.Tests.ChangeRequests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ChangeFlow.Tests.ChangeRequests.Integration;

public sealed class DraftConcurrencyTests(ChangeRequestApiFixture fixture)
    : IClassFixture<ChangeRequestApiFixture>
{
    [Fact]
    public async Task Edit_and_submit_on_one_version_commit_exactly_one_change()
    {
        using var editor = fixture.CreateAuthenticatedClient(fixture.Requester);
        using var submitter = fixture.CreateAuthenticatedClient(fixture.Requester);
        using var created = await editor.PostAsJsonAsync("/change-requests",
            new { title = "Original", description = "Detail", reason = "Need" });
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var location = created.Headers.Location!;
        var original = await created.Content.ReadFromJsonAsync<JsonElement>();
        var version = original.GetProperty("version").GetString();

        var start = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var edit = EditAsync();
        var submit = SubmitAsync();
        start.SetResult();
        await Task.WhenAll(edit, submit);
        using var editResponse = await edit;
        using var submitResponse = await submit;
        var editWon = editResponse.StatusCode == HttpStatusCode.OK;
        var winner = editWon ? editResponse : submitResponse;
        var loser = editWon ? submitResponse : editResponse;

        Assert.Equal(HttpStatusCode.OK, winner.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, loser.StatusCode);
        var conflict = await loser.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("version_conflict", conflict.GetProperty("code").GetString());

        var contextFactory = fixture.Factory.Services.GetRequiredService<IDbContextFactory<ChangeFlowDbContext>>();
        await using var context = await contextFactory.CreateDbContextAsync();
        var id = original.GetProperty("id").GetGuid();
        var stored = await context.Set<ChangeRequest>().SingleAsync(request => request.Id == id);
        Assert.Equal(2L, stored.Version);
        Assert.Equal(editWon ? ChangeRequestStatus.Draft : ChangeRequestStatus.PendingReview, stored.Status);
        Assert.Equal(editWon ? "Edited" : "Original", stored.Title);

        async Task<HttpResponseMessage> EditAsync()
        {
            await start.Task;
            return await editor.PutAsJsonAsync(location,
                new { title = "Edited", description = "Detail", reason = "Need", expectedVersion = version });
        }

        async Task<HttpResponseMessage> SubmitAsync()
        {
            await start.Task;
            return await submitter.PostAsJsonAsync($"{location}/submit", new { expectedVersion = version });
        }
    }
}
