using ChangeFlow.Core.Application.Users.CreateUser;
using ChangeFlow.Core.Domain.Users;
using ChangeFlow.Infra.DataAccess.Common;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace ChangeFlow.Infra.DataAccess.Users.Persistence;

public sealed class EfUserProvisioningStore(
    IDbContextFactory<ChangeFlowDbContext> dbContextFactory)
    : IUserProvisioningStore
{
    private static readonly TimeSpan[] RetryDelays =
    [
        TimeSpan.FromMilliseconds(50),
        TimeSpan.FromMilliseconds(100),
        TimeSpan.FromMilliseconds(200)
    ];

    public async Task<CreateUserFailure?> ProvisionAsync(
        User user,
        string passwordHash,
        CancellationToken cancellationToken = default)
    {
        for (var attempt = 0; attempt <= RetryDelays.Length; attempt++)
        {
            try
            {
                await ProvisionOnceAsync(user, passwordHash, cancellationToken);
                return null;
            }
            catch (Exception exception) when (IsLoginIdentifierConflict(exception))
            {
                return new LoginIdentifierConflict();
            }
            catch (Exception exception) when (IsBusyOrLocked(exception))
            {
                if (attempt < RetryDelays.Length)
                {
                    await Task.Delay(RetryDelays[attempt], cancellationToken);
                    continue;
                }

                return await LoginIdentifierExistsAsync(
                    user.LoginIdentifier.Normalized,
                    cancellationToken)
                    ? new LoginIdentifierConflict()
                    : new UnexpectedFailure("persistence", exception.GetType().Name);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception)
            {
                return new UnexpectedFailure("persistence", exception.GetType().Name);
            }
        }

        return new UnexpectedFailure();
    }

    private async Task ProvisionOnceAsync(
        User user,
        string passwordHash,
        CancellationToken cancellationToken)
    {
        await using var context = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);

        context.Set<UserRecord>().Add(new UserRecord
        {
            Id = user.Id.Value,
            DisplayName = user.DisplayName,
            LoginIdentifier = user.LoginIdentifier.Original,
            NormalizedLoginIdentifier = user.LoginIdentifier.Normalized
        });
        context.Set<UserCredentialRecord>().Add(new UserCredentialRecord
        {
            UserId = user.Id.Value,
            PasswordHash = passwordHash
        });
        context.Set<UserRoleRecord>().AddRange(user.Roles.Select(role => new UserRoleRecord
        {
            UserId = user.Id.Value,
            Role = role.ToString()
        }));

        await context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    private async Task<bool> LoginIdentifierExistsAsync(
        string normalizedLoginIdentifier,
        CancellationToken cancellationToken)
    {
        await using var context = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        return await context.Set<UserRecord>().AnyAsync(
            user => user.NormalizedLoginIdentifier == normalizedLoginIdentifier,
            cancellationToken);
    }

    private static bool IsBusyOrLocked(Exception exception)
    {
        var sqliteException = FindSqliteException(exception);
        return sqliteException?.SqliteErrorCode is 5 or 6;
    }

    private static bool IsLoginIdentifierConflict(Exception exception)
    {
        var sqliteException = FindSqliteException(exception);
        return sqliteException?.SqliteErrorCode == 19
            && (sqliteException.Message.Contains(
                    "ux_users_normalized_login_identifier",
                    StringComparison.OrdinalIgnoreCase)
                || sqliteException.Message.Contains(
                    "users.normalized_login_identifier",
                    StringComparison.OrdinalIgnoreCase));
    }

    private static SqliteException? FindSqliteException(Exception exception)
    {
        for (var current = exception; current is not null; current = current.InnerException!)
        {
            if (current is SqliteException sqliteException)
            {
                return sqliteException;
            }
        }

        return null;
    }
}
