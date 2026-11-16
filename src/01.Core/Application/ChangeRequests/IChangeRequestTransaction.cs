using ChangeFlow.Core.Domain.ChangeRequests;
using ChangeFlow.Core.Domain.Users;

namespace ChangeFlow.Core.Application.ChangeRequests;

public interface IChangeRequestTransaction
{
    Task<T> ReadAsync<T>(
        Func<IChangeRequestSession, CancellationToken, Task<T>> read,
        CancellationToken cancellationToken = default);

    // Both callbacks capture the original command, including its expected version.
    // RecheckAfterContention is read-only and runs in a fresh snapshot.
    Task<ChangeRequestResult<T>> WriteAsync<T>(
        Func<IChangeRequestSession, CancellationToken, Task<ChangeRequestResult<T>>> write,
        Func<IChangeRequestSession, CancellationToken, Task<ChangeRequestFailure>> recheckAfterContention,
        CancellationToken cancellationToken = default);
}

public interface IChangeRequestSession
{
    // Null means the local account is absent. An empty set means it has no current roles.
    Task<IReadOnlySet<UserRole>?> ReadRolesAsync(
        UserId userId, CancellationToken cancellationToken);

    Task<ChangeRequest?> FindAsync(Guid id, CancellationToken cancellationToken);
    void Add(ChangeRequest request);
    void AddDecision(ReviewDecision decision);
    Task<bool> HasDecisionAsync(Guid id, UserId reviewer, CancellationToken cancellationToken);
    Task<IReadOnlyList<ReviewDecision>> ReadHistoryAsync(Guid id, CancellationToken cancellationToken);
    Task<string?> ReadLatestFeedbackAsync(Guid id, CancellationToken cancellationToken);
}
