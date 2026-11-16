using ChangeFlow.Core.Application.ChangeRequests;
using ChangeFlow.Core.Domain.ChangeRequests;
using ChangeFlow.Core.Domain.Users;

namespace ChangeFlow.Tests.ChangeRequests.Application;

public sealed class ReviewAuthorizationTests
{
    [Fact]
    public async Task Review_and_history_use_the_current_roles_from_each_transaction()
    {
        var owner = UserId.New();
        var reviewer = UserId.New();
        var draft = ChangeRequest.Create(owner, new DraftContent("Title", "Detail", "Reason"));
        Assert.True(draft.Submit());
        var session = new Session(draft, owner, reviewer);
        var workflow = new ReviewWorkflow(session, TimeProvider.System);

        var self = await workflow.ApproveAsync(owner, draft.Id, 2);
        Assert.Equal(ChangeRequestFailure.SelfReviewForbidden, self.Failure);
        Assert.Equal(ChangeRequestFailure.Forbidden,
            (await workflow.HistoryAsync(reviewer, draft.Id)).Failure);
        var approved = await workflow.ApproveAsync(reviewer, draft.Id, 2);
        Assert.Null(approved.Failure);
        Assert.Single(session.Decisions);
        Assert.Single((await workflow.HistoryAsync(reviewer, draft.Id)).Value!);

        session.ReviewerRoles.Clear();
        Assert.Equal(ChangeRequestFailure.Forbidden,
            (await workflow.HistoryAsync(reviewer, draft.Id)).Failure);
        Assert.Equal(ChangeRequestFailure.Forbidden, (await workflow.ApproveAsync(reviewer, draft.Id, 2)).Failure);
        Assert.Single((await workflow.HistoryAsync(owner, draft.Id)).Value!);
        Assert.Equal(3L, draft.Version);
    }

    private sealed class Session(ChangeRequest request, UserId owner, UserId reviewer)
        : IChangeRequestSession, IChangeRequestTransaction
    {
        public HashSet<UserRole> ReviewerRoles { get; } = [UserRole.Reviewer];
        public List<ReviewDecision> Decisions { get; } = [];
        public Task<IReadOnlySet<UserRole>?> ReadRolesAsync(UserId actor, CancellationToken token) =>
            Task.FromResult<IReadOnlySet<UserRole>?>(actor == owner
                ? new HashSet<UserRole> { UserRole.Requester, UserRole.Reviewer }
                : actor == reviewer ? ReviewerRoles : null);
        public Task<ChangeRequest?> FindAsync(Guid id, CancellationToken token) =>
            Task.FromResult<ChangeRequest?>(id == request.Id ? request : null);
        public void Add(ChangeRequest draft) => throw new InvalidOperationException();
        public void AddDecision(ReviewDecision decision) => Decisions.Add(decision);
        public Task<bool> HasDecisionAsync(Guid id, UserId actor, CancellationToken token) =>
            Task.FromResult(Decisions.Any(decision => decision.ChangeRequestId == id && decision.ReviewerId == actor));
        public Task<IReadOnlyList<ReviewDecision>> ReadHistoryAsync(Guid id, CancellationToken token) =>
            Task.FromResult<IReadOnlyList<ReviewDecision>>(Decisions.Where(decision => decision.ChangeRequestId == id).ToArray());
        public Task<string?> ReadLatestFeedbackAsync(Guid id, CancellationToken token) =>
            Task.FromResult(Decisions.LastOrDefault(decision => decision.ChangeRequestId == id)?.Feedback);
        public Task<T> ReadAsync<T>(Func<IChangeRequestSession, CancellationToken, Task<T>> read,
            CancellationToken cancellationToken = default) => read(this, cancellationToken);
        public Task<ChangeRequestResult<T>> WriteAsync<T>(
            Func<IChangeRequestSession, CancellationToken, Task<ChangeRequestResult<T>>> write,
            Func<IChangeRequestSession, CancellationToken, Task<ChangeRequestFailure>> recheckAfterContention,
            CancellationToken cancellationToken = default) => write(this, cancellationToken);
    }
}
