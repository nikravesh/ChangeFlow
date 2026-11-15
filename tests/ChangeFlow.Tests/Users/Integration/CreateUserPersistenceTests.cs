using ChangeFlow.Core.Domain.Users;
using ChangeFlow.Infra.DataAccess.Users.Persistence;
using ChangeFlow.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace ChangeFlow.Tests.Users.Integration;

public sealed class CreateUserPersistenceTests
{
    [Fact]
    public async Task Store_commits_user_credential_and_roles_atomically_without_plaintext()
    {
        var fixture = new SqliteDatabaseFixture();
        var plaintext = TestSecretFactory.CreateValidPassword();
        const string passwordHash = "versioned-opaque-hash";

        try
        {
            await fixture.InitializeAsync();
            var store = new EfUserProvisioningStore(
                new TestDbContextFactory(fixture));
            var user = User.Create(
                "Alice",
                "alice",
                [UserRole.Requester, UserRole.Reviewer]);

            await store.ProvisionAsync(user, passwordHash);

            await using var context = fixture.CreateDbContext();
            var connection = context.Database.GetDbConnection();
            await connection.OpenAsync();
            await using var command = connection.CreateCommand();
            command.CommandText = """
                SELECT
                    (SELECT COUNT(*) FROM users),
                    (SELECT password_hash FROM user_credentials LIMIT 1),
                    (SELECT COUNT(*) FROM user_roles);
                """;
            await using var reader = await command.ExecuteReaderAsync();
            Assert.True(await reader.ReadAsync());
            Assert.Equal(1L, reader.GetInt64(0));
            Assert.Equal(passwordHash, reader.GetString(1));
            Assert.DoesNotContain(plaintext, reader.GetString(1), StringComparison.Ordinal);
            Assert.Equal(2L, reader.GetInt64(2));
        }
        finally
        {
            await fixture.DisposeAsync();
        }
    }
}
