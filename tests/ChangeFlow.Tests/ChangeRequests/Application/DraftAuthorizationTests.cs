using ChangeFlow.Core.Application.ChangeRequests;
using ChangeFlow.Core.Domain.ChangeRequests;
using ChangeFlow.Core.Domain.Users;

namespace ChangeFlow.Tests.ChangeRequests.Application;

public sealed class DraftAuthorizationTests
{
    [Theory]
    [InlineData("non-owner", ChangeRequestFailure.Forbidden)]
    [InlineData("no-role", ChangeRequestFailure.Forbidden)]
    [InlineData("unknown", ChangeRequestFailure.AuthenticationRequired)]
    [InlineData("stale", ChangeRequestFailure.VersionConflict)]
    public async Task Rejected_edit_and_submit_leave_the_draft_unchanged(
        string scenario, ChangeRequestFailure expectedFailure)
    {
        var owner = UserId.New();
        var actor = scenario == "non-owner" ? UserId.New() : owner;
        var draft = ChangeRequest.Create(owner, new DraftContent("Original", "Detail", "Reason"));
        IReadOnlySet<UserRole>? roles = scenario switch
        {
            "unknown" => null,
            "no-role" => new HashSet<UserRole>(),
            _ => new HashSet<UserRole> { UserRole.Requester }
        };
        var session = new DraftSession(draft, roles);
        var workflow = new DraftWorkflow(session);
        var version = scenario == "stale" ? 2L : 1L;

        var edit = await workflow.EditAsync(actor, draft.Id, version,
            new DraftContent("Changed", "Detail", "Reason"));
        var submit = await workflow.SubmitAsync(actor, draft.Id, version);

        Assert.Equal(expectedFailure, edit.Failure);
        Assert.Equal(expectedFailure, submit.Failure);
        Assert.Equal((ChangeRequestStatus.Draft, 1L, "Original"), (draft.Status, draft.Version, draft.Title));
        Assert.Equal(0, session.SavedWrites);
    }

    internal sealed class DraftSession(ChangeRequest draft, IReadOnlySet<UserRole>? roles, string? feedback = null)
        : IChangeRequestSession, IChangeRequestTransaction
    {
        public int SavedWrites { get; private set; }

        public Task<IReadOnlySet<UserRole>?> ReadRolesAsync(UserId userId, CancellationToken cancellationToken) =>
            Task.FromResult(roles);

        public Task<ChangeRequest?> FindAsync(Guid id, CancellationToken cancellationToken) =>
            Task.FromResult<ChangeRequest?>(id == draft.Id ? draft : null);

        public void Add(ChangeRequest request) => throw new InvalidOperationException();
        public void AddDecision(ReviewDecision decision) => throw new InvalidOperationException();
        public Task<bool> HasDecisionAsync(Guid id, UserId reviewer, CancellationToken cancellationToken) =>
            throw new InvalidOperationException();
        public Task<IReadOnlyList<ReviewDecision>> ReadHistoryAsync(Guid id, CancellationToken cancellationToken) =>
            throw new InvalidOperationException();
        public Task<string?> ReadLatestFeedbackAsync(Guid id, CancellationToken cancellationToken) =>
            Task.FromResult(feedback);

        public Task<T> ReadAsync<T>(Func<IChangeRequestSession, CancellationToken, Task<T>> read,
            CancellationToken cancellationToken = default) => read(this, cancellationToken);

        public async Task<ChangeRequestResult<T>> WriteAsync<T>(
            Func<IChangeRequestSession, CancellationToken, Task<ChangeRequestResult<T>>> write,
            Func<IChangeRequestSession, CancellationToken, Task<ChangeRequestFailure>> recheckAfterContention,
            CancellationToken cancellationToken = default)
        {
            var result = await write(this, cancellationToken);
            if (result.Failure is null)
            {
                SavedWrites++;
            }

            return result;
        }
    }
}
