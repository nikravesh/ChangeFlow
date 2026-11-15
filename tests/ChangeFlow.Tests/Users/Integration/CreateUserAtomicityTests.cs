using ChangeFlow.Core.Application.Users.CreateUser;
using ChangeFlow.Core.Domain.Users;
using ChangeFlow.Infra.DataAccess.Users.Persistence;
using ChangeFlow.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace ChangeFlow.Tests.Users.Integration;

public sealed class CreateUserAtomicityTests
{
    [Theory]
    [InlineData("user_credentials")]
    [InlineData("user_roles")]
    public async Task Failure_while_writing_a_dependent_row_rolls_back_every_table(string table)
    {
        var fixture = new SqliteDatabaseFixture();

        try
        {
            await fixture.InitializeAsync();
            await AddFailingInsertTrigger(fixture, table);
            var store = new EfUserProvisioningStore(new TestDbContextFactory(fixture));
            var user = User.Create("Alice", $"alice-{table}", [UserRole.Requester]);

            var failure = await store.ProvisionAsync(user, "opaque-hash");
            var counts = await Counts(fixture);

            Assert.IsType<UnexpectedFailure>(failure);
            Assert.Equal([0L, 0L, 0L], counts);
        }
        finally
        {
            await fixture.DisposeAsync();
        }
    }

    private static async Task AddFailingInsertTrigger(SqliteDatabaseFixture fixture, string table)
    {
        var sql = table switch
        {
            "user_credentials" => """
                CREATE TRIGGER fail_user_credentials_insert
                BEFORE INSERT ON user_credentials
                BEGIN
                    SELECT RAISE(ABORT, 'controlled test failure');
                END;
                """,
            "user_roles" => """
                CREATE TRIGGER fail_user_roles_insert
                BEFORE INSERT ON user_roles
                BEGIN
                    SELECT RAISE(ABORT, 'controlled test failure');
                END;
                """,
            _ => throw new ArgumentOutOfRangeException(nameof(table))
        };
        await using var context = fixture.CreateDbContext();
        await context.Database.ExecuteSqlRawAsync(sql);
    }

    private static async Task<long[]> Counts(SqliteDatabaseFixture fixture)
    {
        await using var context = fixture.CreateDbContext();
        var connection = context.Database.GetDbConnection();
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT
                (SELECT COUNT(*) FROM users),
                (SELECT COUNT(*) FROM user_credentials),
                (SELECT COUNT(*) FROM user_roles);
            """;
        await using var reader = await command.ExecuteReaderAsync();
        Assert.True(await reader.ReadAsync());
        return [reader.GetInt64(0), reader.GetInt64(1), reader.GetInt64(2)];
    }
}
