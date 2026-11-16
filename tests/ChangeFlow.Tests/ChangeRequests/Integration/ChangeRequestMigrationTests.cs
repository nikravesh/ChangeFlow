using ChangeFlow.Core.Application.ChangeRequests;
using ChangeFlow.Core.Domain.ChangeRequests;
using ChangeFlow.Core.Domain.Users;
using ChangeFlow.Infra.DataAccess.ChangeRequests;
using ChangeFlow.Infra.DataAccess.Common;
using ChangeFlow.Infra.DataAccess.Migrations;
using ChangeFlow.Infra.DataAccess.Users.Persistence;
using ChangeFlow.Infra.DataAccess.Users.Security;
using ChangeFlow.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace ChangeFlow.Tests.ChangeRequests.Integration;

public sealed class ChangeRequestMigrationTests
{
    private const string InitialMigration = "202611140001_InitialUserProvisioning";

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Migrations_preserve_users_roles_requests_and_decisions_on_reapplication(bool upgradeExisting)
    {
        var database = new SqliteDatabaseFixture();
        try
        {
            var contextFactory = new TestDbContextFactory(database);
            var owner = User.Create("Migration Owner", "migration.owner", [UserRole.Requester]);
            var reviewer = User.Create("Migration Reviewer", "migration.reviewer", [UserRole.Reviewer]);
            var users = new EfUserProvisioningStore(contextFactory);
            var hasher = new IdentityPasswordCredentialHasher();

            async Task ProvisionAsync(User user) => Assert.Null(await users.ProvisionAsync(
                user, hasher.Hash(user, TestSecretFactory.CreateValidPassword())));

            await using (var context = database.CreateDbContext())
            {
                if (upgradeExisting)
                {
                    await ApplyLegacyMigrationAsync(context);
                    await ProvisionAsync(owner);
                    await ProvisionAsync(reviewer);
                    Assert.Equal(new[] { InitialMigration }, await context.Database.GetAppliedMigrationsAsync());
                }

                await context.Database.MigrateAsync();
                Assert.Empty(await context.Database.GetPendingMigrationsAsync());
                Assert.Equal(new[]
                {
                    InitialMigration,
                    "202611160001_AddChangeRequests",
                    "202611160002_AddReviewDecisions"
                }, await context.Database.GetAppliedMigrationsAsync());
            }

            if (!upgradeExisting)
            {
                await ProvisionAsync(owner);
                await ProvisionAsync(reviewer);
            }

            var transaction = new EfChangeRequestTransaction(contextFactory);
            var draft = new DraftWorkflow(transaction);
            var created = await draft.CreateAsync(owner.Id, new DraftContent("Migration", "Detail", "Reason"));
            Assert.Null(created.Failure);
            var id = created.Value!.Id;
            Assert.Null((await draft.SubmitAsync(owner.Id, id, 1)).Failure);
            var review = new ReviewWorkflow(transaction, TimeProvider.System);
            Assert.Null((await review.RequestChangesAsync(reviewer.Id, id, 2, "Saved feedback")).Failure);

            await using (var reopened = database.CreateDbContext())
            {
                await reopened.Database.MigrateAsync();
                var saved = await reopened.Set<ChangeRequest>().SingleAsync();
                Assert.Equal((id, owner.Id, ChangeRequestStatus.ChangesRequested, 3L, "Migration"),
                    (saved.Id, saved.RequesterId, saved.Status, saved.Version, saved.Title));
                var decision = await reopened.Set<ReviewDecision>().SingleAsync();
                Assert.Equal((id, reviewer.Id, 3L, ReviewAction.RequestChanges, "Saved feedback"),
                    (decision.ChangeRequestId, decision.ReviewerId, decision.ResultingVersion,
                        decision.Action, decision.Feedback));
            }

            var roles = await transaction.ReadAsync(async (session, token) =>
                (Owner: await session.ReadRolesAsync(owner.Id, token),
                    Reviewer: await session.ReadRolesAsync(reviewer.Id, token)));
            Assert.Equal(UserRole.Requester, Assert.Single(roles.Owner!));
            Assert.Equal(UserRole.Reviewer, Assert.Single(roles.Reviewer!));
        }
        finally
        {
            await database.DisposeAsync();
        }
    }

    private static async Task ApplyLegacyMigrationAsync(ChangeFlowDbContext context)
    {
        // EF's targeted migration lookup requires a 14-digit prefix. Preserve the
        // existing migration ID and prepare the legacy file using its actual operations.
        var migration = new InitialUserProvisioning
        {
            ActiveProvider = context.Database.ProviderName
                ?? throw new InvalidOperationException("The legacy fixture requires a configured provider.")
        };
        var sqlGenerator = context.GetService<IMigrationsSqlGenerator>();
        var history = context.GetService<IHistoryRepository>();
        await using var transaction = await context.Database.BeginTransactionAsync();
        await context.Database.ExecuteSqlRawAsync(history.GetCreateScript());
        foreach (var command in sqlGenerator.Generate(migration.UpOperations))
        {
            await context.Database.ExecuteSqlRawAsync(command.CommandText);
        }

        await context.Database.ExecuteSqlRawAsync(history.GetInsertScript(new HistoryRow(InitialMigration, "11.0.0")));
        await transaction.CommitAsync();
    }
}
