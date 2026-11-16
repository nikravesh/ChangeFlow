using ChangeFlow.Core.Domain.ChangeRequests;
using ChangeFlow.Core.Domain.Users;

namespace ChangeFlow.Tests.ChangeRequests.Domain;

public sealed class DraftWorkflowTests
{
    [Fact]
    public void Draft_has_generated_identity_and_explicit_transitions()
    {
        var owner = UserId.New();
        var draft = ChangeRequest.Create(owner, new DraftContent(" Title ", " Detail ", " Reason "));

        Assert.NotEqual(Guid.Empty, draft.Id);
        Assert.Equal(owner, draft.RequesterId);
        Assert.Equal("Title", draft.Title);
        Assert.Equal("Detail", draft.Description);
        Assert.Equal("Reason", draft.Reason);
        Assert.Equal(ChangeRequestStatus.Draft, draft.Status);
        Assert.Equal(1L, draft.Version);
        Assert.True(draft.Edit(new DraftContent("New", "Detail", "Reason")));
        Assert.Equal(2L, draft.Version);
        Assert.True(draft.Submit());
        Assert.Equal(ChangeRequestStatus.PendingReview, draft.Status);
        Assert.False(draft.Edit(new DraftContent("Rejected", "Detail", "Reason")));
        Assert.False(draft.Submit());
        Assert.Equal((3L, "New"), (draft.Version, draft.Title));
    }

    [Theory]
    [InlineData(" ", "Detail", "Reason")]
    [InlineData("Title", "\t", "Reason")]
    [InlineData("Title", "Detail", "\n")]
    public void Whitespace_is_rejected(string title, string description, string reason) =>
        Assert.Throws<ArgumentException>(() => new DraftContent(title, description, reason));
}
