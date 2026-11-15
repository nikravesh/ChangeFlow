using System.Security.Cryptography;
using System.Text;
using ChangeFlow.Infra.DataAccess.Common;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace ChangeFlow.Tests.Infrastructure;

public sealed class ChangeFlowApiFactory : WebApplicationFactory<Program>
{
    private readonly string databasePath = Path.Combine(
        Path.GetTempPath(),
        $"changeflow-api-tests-{Guid.NewGuid():N}.db");

    public ChangeFlowApiFactory()
    {
        SetupCode = TestSecretFactory.CreateSetupCode();
    }

    public string SetupCode { get; }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        var connectionString = CreateConnectionString();

        builder.ConfigureAppConfiguration((_, configuration) =>
        {
            var setupCodeDigest = Convert.ToHexString(
                SHA256.HashData(Encoding.UTF8.GetBytes(SetupCode)));

            configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:ChangeFlow"] = connectionString,
                ["Bootstrap:SetupCodeDigest"] = setupCodeDigest
            });
        });
        builder.ConfigureServices(services =>
        {
            services.RemoveAll<IDbContextFactory<ChangeFlowDbContext>>();
            services.AddSingleton<IDbContextFactory<ChangeFlowDbContext>>(
                new FileDbContextFactory(connectionString));
        });
    }

    private string CreateConnectionString() => new SqliteConnectionStringBuilder
    {
        DataSource = databasePath,
        ForeignKeys = true,
        DefaultTimeout = 5,
        Pooling = true
    }.ToString();

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);

        if (!disposing)
        {
            return;
        }

        SqliteConnection.ClearAllPools();

        if (File.Exists(databasePath))
        {
            File.Delete(databasePath);
        }
    }

    private sealed class FileDbContextFactory(string connectionString)
        : IDbContextFactory<ChangeFlowDbContext>
    {
        public ChangeFlowDbContext CreateDbContext()
        {
            var options = new DbContextOptionsBuilder<ChangeFlowDbContext>()
                .UseSqlite(connectionString)
                .Options;

            return new ChangeFlowDbContext(options);
        }

        public Task<ChangeFlowDbContext> CreateDbContextAsync(
            CancellationToken cancellationToken = default) =>
            Task.FromResult(CreateDbContext());
    }
}
