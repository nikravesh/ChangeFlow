using ChangeFlow.Core.Domain.ChangeRequests;
using ChangeFlow.Infra.DataAccess.Common;
using ChangeFlow.Tests.ChangeRequests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using static ChangeFlow.Tests.ChangeRequests.Contract.ResubmissionContractTests;
using static ChangeFlow.Tests.ChangeRequests.Contract.ReviewWorkflowContractTests;

namespace ChangeFlow.Tests.ChangeRequests.Integration;

public sealed class ResubmissionPersistenceTests(ChangeRequestApiFixture fixture)
    : IClassFixture<ChangeRequestApiFixture>
{
    [Fact]
    public async Task Restart_preserves_the_final_request_and_both_decisions()
    {
        string path;
        using (var owner = fixture.CreateAuthenticatedClient(fixture.Requester))
        using (var reviewer = fixture.CreateAuthenticatedClient(fixture.Reviewer))
        {
            path = await CreatePendingAsync(owner);
            await ReviseAndApproveAsync(owner, reviewer, path, fixture.Requester.Value, fixture.SecondReviewer.Value);
        }

        var id = Guid.Parse(path[(path.LastIndexOf('/') + 1)..]);
        var before = await ReadStoredAsync(id);
        Assert.Equal(ChangeRequestStatus.Approved, before.Request.Status);
        Assert.Equal(6L, before.Request.Version);
        Assert.Equal(new long[] { 3, 6 }, before.Decisions.Select(decision => decision.Version));
        Assert.Equal(new[] { ReviewAction.RequestChanges, ReviewAction.Approve },
            before.Decisions.Select(decision => decision.Action));

        await fixture.RestartAsync();
        using var restartedOwner = fixture.CreateAuthenticatedClient(fixture.Requester);
        using var restartedReviewer = fixture.CreateAuthenticatedClient(fixture.Reviewer);
        var snapshot = await ReadStoredAsync(id);
        Assert.Equal(before.Request, snapshot.Request);
        Assert.Equal(before.Decisions, snapshot.Decisions);
        await AssertApprovedCommandsRejectedAsync(restartedOwner, restartedReviewer, path);
        var after = await ReadStoredAsync(id);
        Assert.Equal(snapshot.Request, after.Request);
        Assert.Equal(snapshot.Decisions, after.Decisions);
    }

    private async Task<(StoredRequest Request, StoredDecision[] Decisions)> ReadStoredAsync(Guid id)
    {
        var factory = fixture.Factory.Services.GetRequiredService<IDbContextFactory<ChangeFlowDbContext>>();
        await using var context = await factory.CreateDbContextAsync();
        var request = await context.Set<ChangeRequest>().AsNoTracking().SingleAsync(request => request.Id == id);
        var decisions = await context.Set<ReviewDecision>().AsNoTracking()
            .Where(decision => decision.ChangeRequestId == id)
            .OrderBy(decision => decision.ResultingVersion)
            .ToListAsync();
        return (new(request.RequesterId.Value, request.Title, request.Description, request.Reason,
            request.Status, request.Version), decisions.Select(decision => new StoredDecision(
                decision.ResultingVersion, decision.ReviewerId.Value, decision.DecidedAtUtc,
                decision.Action, decision.Feedback)).ToArray());
    }

    private sealed record StoredRequest(Guid Requester, string Title, string Description, string Reason,
        ChangeRequestStatus Status, long Version);

    private sealed record StoredDecision(long Version, Guid Reviewer, DateTimeOffset DecidedAt,
        ReviewAction Action, string? Feedback);
}
