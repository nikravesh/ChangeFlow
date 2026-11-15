using ChangeFlow.Infra.DataAccess.Common;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace ChangeFlow.Tests.Infrastructure;

public sealed class SqliteDatabaseFixture : IAsyncLifetime
{
    private readonly string databasePath = Path.Combine(
        Path.GetTempPath(),
        $"changeflow-tests-{Guid.NewGuid():N}.db");

    public string ConnectionString => new SqliteConnectionStringBuilder
    {
        DataSource = databasePath,
        ForeignKeys = true,
        DefaultTimeout = 5,
        Pooling = true
    }.ToString();

    public ChangeFlowDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<ChangeFlowDbContext>()
            .UseSqlite(ConnectionString)
            .Options;

        return new ChangeFlowDbContext(options);
    }

    public async Task InitializeAsync()
    {
        await using var context = CreateDbContext();
        await context.Database.EnsureCreatedAsync();
    }

    public Task DisposeAsync()
    {
        SqliteConnection.ClearAllPools();

        if (File.Exists(databasePath))
        {
            File.Delete(databasePath);
        }

        return Task.CompletedTask;
    }
}
