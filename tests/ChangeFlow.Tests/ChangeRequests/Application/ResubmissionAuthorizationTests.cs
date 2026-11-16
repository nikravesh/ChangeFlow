using ChangeFlow.Core.Application.ChangeRequests;
using ChangeFlow.Core.Domain.ChangeRequests;
using ChangeFlow.Core.Domain.Users;

namespace ChangeFlow.Tests.ChangeRequests.Application;

public sealed class ResubmissionAuthorizationTests
{
    [Theory]
    [InlineData(true, true)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public async Task Only_owner_with_current_requester_role_can_edit_and_resubmit(bool isOwner, bool hasRole)
    {
        var owner = UserId.New();
        var actor = isOwner ? owner : UserId.New();
        var draft = ChangeRequest.Create(owner, new DraftContent("Initial", "Detail", "Reason"));
        Assert.True(draft.Submit());
        Assert.NotNull(draft.RequestChanges(UserId.New(), "Revise", DateTimeOffset.UtcNow));
        var roles = new HashSet<UserRole>();
        if (hasRole)
        {
            roles.Add(UserRole.Requester);
        }

        var session = new DraftAuthorizationTests.DraftSession(draft, roles, "Revise");
        var workflow = new DraftWorkflow(session);
        var edit = await workflow.EditAsync(actor, draft.Id, 3, new DraftContent("Edited", "Detail", "Reason"));
        var submit = await workflow.SubmitAsync(actor, draft.Id, isOwner && hasRole ? 4 : 3);

        if (isOwner && hasRole)
        {
            Assert.Null(edit.Failure);
            Assert.Equal("Revise", edit.Value!.CurrentFeedback);
            Assert.Null(submit.Failure);
            Assert.Null(submit.Value!.CurrentFeedback);
            Assert.Equal((ChangeRequestStatus.PendingReview, 5L, "Edited", 2),
                (draft.Status, draft.Version, draft.Title, session.SavedWrites));
        }
        else
        {
            Assert.Equal(ChangeRequestFailure.Forbidden, edit.Failure);
            Assert.Equal(ChangeRequestFailure.Forbidden, submit.Failure);
            Assert.Equal((ChangeRequestStatus.ChangesRequested, 3L, "Initial", 0),
                (draft.Status, draft.Version, draft.Title, session.SavedWrites));
        }
    }
}
