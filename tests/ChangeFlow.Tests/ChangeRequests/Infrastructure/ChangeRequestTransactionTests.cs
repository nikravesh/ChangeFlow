using ChangeFlow.Core.Application.ChangeRequests;
using ChangeFlow.Core.Domain.Users;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;

namespace ChangeFlow.Tests.ChangeRequests.Infrastructure;

public sealed class ChangeRequestTransactionTests(ChangeRequestApiFixture fixture)
    : IClassFixture<ChangeRequestApiFixture>
{
    [Theory]
    [InlineData(true, 2, ChangeRequestFailure.VersionConflict)]
    [InlineData(true, 1, ChangeRequestFailure.TemporarilyUnavailable)]
    [InlineData(false, 2, ChangeRequestFailure.Forbidden)]
    public async Task Exhausted_busy_retry_rechecks_access_and_version_in_a_fresh_snapshot(
        bool isRequester, long currentVersion, ChangeRequestFailure expectedFailure)
    {
        await using var scope = fixture.Factory.Services.CreateAsyncScope();
        var transaction = scope.ServiceProvider.GetRequiredService<IChangeRequestTransaction>();
        var user = isRequester ? fixture.Requester : fixture.Reviewer;
        var attempts = new List<IChangeRequestSession>();
        const long expectedVersion = 1;

        var result = await transaction.WriteAsync<int>(
            (session, _) =>
            {
                attempts.Add(session);
                throw new SqliteException("Simulated SQLite writer contention.", 5);
            },
            async (session, cancellationToken) =>
            {
                Assert.DoesNotContain(session, attempts);
                var roles = await session.ReadRolesAsync(user, cancellationToken);
                return ChangeRequestAccess.ResolveContention(
                    ChangeRequestAccess.RequireRole(roles, UserRole.Requester),
                    expectedVersion,
                    currentVersion);
            });

        Assert.Equal(4, attempts.Distinct().Count());
        Assert.Equal(expectedFailure, result.Failure);
    }
}
