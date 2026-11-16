using ChangeFlow.Core.Domain.ChangeRequests;
using ChangeFlow.Core.Domain.Users;

namespace ChangeFlow.Tests.ChangeRequests.Domain;

public sealed class ResubmissionTests
{
    [Fact]
    public void Resubmission_preserves_owner_and_approved_request_rejects_all_changes()
    {
        var owner = UserId.New();
        var reviewer = UserId.New();
        var draft = ChangeRequest.Create(owner, new DraftContent("Initial", "Detail", "Reason"));
        Assert.True(draft.Submit());
        var initialDecision = draft.RequestChanges(reviewer, "Revise", DateTimeOffset.UtcNow);
        Assert.NotNull(initialDecision);

        Assert.True(draft.Edit(new DraftContent("Revised", "Detail", "Reason")));
        Assert.Equal((owner, ChangeRequestStatus.ChangesRequested, 4L), (draft.RequesterId, draft.Status, draft.Version));
        Assert.True(draft.Submit());
        Assert.Equal((ChangeRequestStatus.PendingReview, 5L), (draft.Status, draft.Version));
        Assert.NotNull(draft.Approve(reviewer, DateTimeOffset.UtcNow));

        Assert.False(draft.Edit(new DraftContent("Rejected", "Changed", "Changed")));
        Assert.False(draft.Submit());
        Assert.Null(draft.Approve(reviewer, DateTimeOffset.UtcNow));
        Assert.Null(draft.RequestChanges(reviewer, "Rejected", DateTimeOffset.UtcNow));
        Assert.Equal((owner, ChangeRequestStatus.Approved, 6L, "Revised"),
            (draft.RequesterId, draft.Status, draft.Version, draft.Title));
        Assert.Equal((3L, "Revise"), (initialDecision.ResultingVersion, initialDecision.Feedback));
    }
}
