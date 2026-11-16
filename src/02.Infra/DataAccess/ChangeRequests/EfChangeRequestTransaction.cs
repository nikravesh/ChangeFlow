using ChangeFlow.Core.Application.ChangeRequests;
using ChangeFlow.Core.Domain.ChangeRequests;
using ChangeFlow.Core.Domain.Users;
using ChangeFlow.Infra.DataAccess.Common;
using ChangeFlow.Infra.DataAccess.Users.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace ChangeFlow.Infra.DataAccess.ChangeRequests;

public sealed class EfChangeRequestTransaction(
    IDbContextFactory<ChangeFlowDbContext> contextFactory) : IChangeRequestTransaction
{
    private static readonly TimeSpan[] RetryDelays =
    [
        TimeSpan.FromMilliseconds(50),
        TimeSpan.FromMilliseconds(100),
        TimeSpan.FromMilliseconds(200)
    ];

    public async Task<T> ReadAsync<T>(
        Func<IChangeRequestSession, CancellationToken, Task<T>> read,
        CancellationToken cancellationToken = default)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        await context.Database.OpenConnectionAsync(cancellationToken);
        var connection = (SqliteConnection)context.Database.GetDbConnection();
        await using var transaction = connection.BeginTransaction(deferred: true);
        await context.Database.UseTransactionAsync(transaction, cancellationToken);
        return await read(new Session(context), cancellationToken);
    }

    public async Task<ChangeRequestResult<T>> WriteAsync<T>(
        Func<IChangeRequestSession, CancellationToken, Task<ChangeRequestResult<T>>> write,
        Func<IChangeRequestSession, CancellationToken, Task<ChangeRequestFailure>> recheckAfterContention,
        CancellationToken cancellationToken = default)
    {
        for (var attempt = 0; ; attempt++)
        {
            var result = await TryWriteAsync(write, cancellationToken);
            if (result is not null)
            {
                return result;
            }

            if (attempt == RetryDelays.Length)
            {
                break;
            }

            await Task.Delay(RetryDelays[attempt], cancellationToken);
        }

        try
        {
            var failure = await ReadAsync(recheckAfterContention, cancellationToken);
            return ChangeRequestResult<T>.Rejected(failure);
        }
        catch (Exception exception) when (IsBusyOrLocked(exception))
        {
            return ChangeRequestResult<T>.Rejected(ChangeRequestFailure.TemporarilyUnavailable);
        }
    }

    // Null denotes contention before commit with no transaction changes left to persist.
    private async Task<ChangeRequestResult<T>?> TryWriteAsync<T>(
        Func<IChangeRequestSession, CancellationToken, Task<ChangeRequestResult<T>>> write,
        CancellationToken cancellationToken)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        SqliteTransaction transaction;
        try
        {
            await context.Database.OpenConnectionAsync(cancellationToken);
            var connection = (SqliteConnection)context.Database.GetDbConnection();
            transaction = connection.BeginTransaction(deferred: false);
        }
        catch (SqliteException exception) when (IsBusyOrLocked(exception))
        {
            return null;
        }

        await using var transactionLease = transaction;
        ChangeRequestResult<T> result;
        try
        {
            await context.Database.UseTransactionAsync(transaction, cancellationToken);
            result = await write(new Session(context), cancellationToken);
            if (result.Failure is not null)
            {
                return result;
            }

            await context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            await transaction.RollbackAsync(CancellationToken.None);
            return ChangeRequestResult<T>.Rejected(ChangeRequestFailure.VersionConflict);
        }
        catch (Exception exception) when (IsBusyOrLocked(exception))
        {
            await transaction.RollbackAsync(CancellationToken.None);
            return null;
        }

        // Commit failures propagate because the outcome can be unknown.
        await transaction.CommitAsync(cancellationToken);
        return result;
    }

    private static bool IsBusyOrLocked(Exception exception) =>
        exception is SqliteException { SqliteErrorCode: 5 or 6 }
        || exception is DbUpdateException { InnerException: SqliteException { SqliteErrorCode: 5 or 6 } };

    private sealed class Session(ChangeFlowDbContext context) : IChangeRequestSession
    {
        public Task<ChangeRequest?> FindAsync(Guid id, CancellationToken cancellationToken) =>
            context.Set<ChangeRequest>().SingleOrDefaultAsync(request => request.Id == id, cancellationToken);

        public void Add(ChangeRequest request) => context.Set<ChangeRequest>().Add(request);

        public void AddDecision(ReviewDecision decision) => context.Set<ReviewDecision>().Add(decision);

        public Task<bool> HasDecisionAsync(Guid id, UserId reviewer, CancellationToken cancellationToken) =>
            context.Set<ReviewDecision>().AnyAsync(decision => decision.ChangeRequestId == id
                && EF.Property<Guid>(decision, "ReviewerKey") == reviewer.Value, cancellationToken);

        public async Task<IReadOnlyList<ReviewDecision>> ReadHistoryAsync(
            Guid id, CancellationToken cancellationToken) =>
            await context.Set<ReviewDecision>().AsNoTracking()
                .Where(decision => decision.ChangeRequestId == id)
                .OrderBy(decision => decision.ResultingVersion)
                .ToListAsync(cancellationToken);

        public Task<string?> ReadLatestFeedbackAsync(Guid id, CancellationToken cancellationToken) =>
            context.Set<ReviewDecision>()
                .Where(decision => decision.ChangeRequestId == id && decision.Action == ReviewAction.RequestChanges)
                .OrderByDescending(decision => decision.ResultingVersion)
                .Select(decision => decision.Feedback)
                .FirstOrDefaultAsync(cancellationToken);

        public async Task<IReadOnlySet<UserRole>?> ReadRolesAsync(
            UserId userId, CancellationToken cancellationToken)
        {
            var exists = await context.Set<UserRecord>().AnyAsync(
                user => user.Id == userId.Value, cancellationToken);
            if (!exists)
            {
                return null;
            }

            var roles = await context.Set<UserRoleRecord>()
                .Where(role => role.UserId == userId.Value)
                .Select(role => role.Role)
                .ToListAsync(cancellationToken);
            return roles.Select(Enum.Parse<UserRole>).ToHashSet();
        }
    }
}
