using ChangeFlow.Infra.DataAccess.Common;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ChangeFlow.Tests.Infrastructure;

public sealed class FoundationSmokeTests
{
    [Fact]
    public async Task Sqlite_fixture_uses_file_backed_database_with_required_connection_settings()
    {
        var fixture = new SqliteDatabaseFixture();

        try
        {
            await fixture.InitializeAsync();

            var connectionSettings = new SqliteConnectionStringBuilder(fixture.ConnectionString);

            Assert.NotEqual(":memory:", connectionSettings.DataSource);
            Assert.True(Path.IsPathFullyQualified(connectionSettings.DataSource));
            Assert.True(connectionSettings.ForeignKeys);
            Assert.Equal(5, connectionSettings.DefaultTimeout);

            await using var context = fixture.CreateDbContext();
            await context.Database.OpenConnectionAsync();
            await using var command = context.Database.GetDbConnection().CreateCommand();
            command.CommandText = "PRAGMA foreign_keys;";

            Assert.Equal(1L, Convert.ToInt64(await command.ExecuteScalarAsync()));
        }
        finally
        {
            await fixture.DisposeAsync();
        }
    }

    [Fact]
    public async Task Api_factory_starts_with_isolated_configuration_and_correlation_header()
    {
        using var firstFactory = new ChangeFlowApiFactory();
        using var secondFactory = new ChangeFlowApiFactory();
        using var client = firstFactory.CreateClient();

        var dbContextFactory = firstFactory.Services
            .GetRequiredService<IDbContextFactory<ChangeFlowDbContext>>();
        await using var firstContext = await dbContextFactory.CreateDbContextAsync();
        await using var secondContext = await dbContextFactory.CreateDbContextAsync();

        Assert.NotSame(firstContext, secondContext);

        using var response = await client.GetAsync("/");

        Assert.Equal(System.Net.HttpStatusCode.NotFound, response.StatusCode);
        Assert.True(response.Headers.TryGetValues("X-Correlation-ID", out var values));
        Assert.False(string.IsNullOrWhiteSpace(values.Single()));
    }
}
