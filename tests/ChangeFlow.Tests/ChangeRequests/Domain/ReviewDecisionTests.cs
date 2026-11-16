using ChangeFlow.Core.Domain.ChangeRequests;
using ChangeFlow.Core.Domain.Users;

namespace ChangeFlow.Tests.ChangeRequests.Domain;

public sealed class ReviewDecisionTests
{
    [Theory]
    [InlineData(false, ChangeRequestStatus.Approved, ReviewAction.Approve, null)]
    [InlineData(true, ChangeRequestStatus.ChangesRequested, ReviewAction.RequestChanges, "Revise")]
    public void Review_creates_one_decision_at_the_resulting_version(
        bool changes, ChangeRequestStatus status, ReviewAction action, string? feedback)
    {
        var request = ChangeRequest.Create(UserId.New(), new DraftContent("Title", "Detail", "Reason"));
        Assert.True(request.Submit());
        var reviewer = UserId.New();
        var now = new DateTimeOffset(2026, 11, 16, 12, 0, 0, TimeSpan.FromHours(3));

        var decision = changes
            ? request.RequestChanges(reviewer, " Revise ", now)
            : request.Approve(reviewer, now);

        Assert.NotNull(decision);
        Assert.Equal((status, 3L), (request.Status, request.Version));
        Assert.Equal((request.Id, 3L, reviewer, now.ToUniversalTime(), action, feedback),
            (decision.ChangeRequestId, decision.ResultingVersion, decision.ReviewerId,
                decision.DecidedAtUtc, decision.Action, decision.Feedback));
        Assert.Null(request.Approve(reviewer, now));
        Assert.Equal(3L, request.Version);
    }

    [Fact]
    public void Invalid_feedback_does_not_change_the_request()
    {
        var request = ChangeRequest.Create(UserId.New(), new DraftContent("Title", "Detail", "Reason"));
        Assert.True(request.Submit());
        Assert.Throws<ArgumentException>(() =>
            request.RequestChanges(UserId.New(), " ", DateTimeOffset.UtcNow));
        Assert.Equal((ChangeRequestStatus.PendingReview, 2L), (request.Status, request.Version));
    }
}
