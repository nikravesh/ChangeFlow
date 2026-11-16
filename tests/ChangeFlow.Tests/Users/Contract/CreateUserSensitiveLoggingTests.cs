using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using ChangeFlow.Core.Application.Users.CreateUser;
using ChangeFlow.Core.Domain.Users;
using ChangeFlow.Infra.DataAccess.Common;
using ChangeFlow.Infra.DataAccess.Users.Persistence;
using ChangeFlow.Endpoints.Api.Common.Observability;
using ChangeFlow.Tests.Infrastructure;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.EntityFrameworkCore;

namespace ChangeFlow.Tests.Users.Contract;

public sealed class CreateUserSensitiveLoggingTests
{
    private const string KnownPasswordHash = "known-sensitive-password-hash";

    [Fact]
    public async Task Create_user_logs_outcome_without_authorization_body_or_secrets()
    {
        var provider = new CapturingLoggerProvider();
        using var baseFactory = new ChangeFlowApiFactory();
        using var factory = baseFactory.WithWebHostBuilder(builder => builder.ConfigureLogging(logging =>
        {
            logging.ClearProviders();
            logging.AddProvider(provider);
        }).ConfigureServices(services =>
        {
            services.RemoveAll<IPasswordCredentialHasher>();
            services.AddSingleton<IPasswordCredentialHasher, KnownHasher>();
        }));
        using var client = factory.CreateClient();
        var password = TestSecretFactory.CreateValidPassword();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Setup", baseFactory.SetupCode);

        using var response = await client.PostAsJsonAsync("/users", new
        {
            displayName = "Sensitive Display Name",
            loginIdentifier = "sensitive.login",
            password,
            roles = new[] { "Requester" }
        });

        response.EnsureSuccessStatusCode();
        var combined = string.Join(Environment.NewLine, provider.Messages);
        Assert.DoesNotContain(baseFactory.SetupCode, combined, StringComparison.Ordinal);
        Assert.DoesNotContain(password, combined, StringComparison.Ordinal);
        Assert.DoesNotContain("Authorization", combined, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Sensitive Display Name", combined, StringComparison.Ordinal);
        Assert.DoesNotContain("sensitive.login", combined, StringComparison.Ordinal);
        Assert.DoesNotContain("passwordHash", combined, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(KnownPasswordHash, combined, StringComparison.Ordinal);
        Assert.Contains("created", combined, StringComparison.OrdinalIgnoreCase);
    }

    private sealed class CapturingLoggerProvider : ILoggerProvider
    {
        public ConcurrentQueue<string> Messages { get; } = new();

        public ILogger CreateLogger(string categoryName) => new CapturingLogger(Messages);

        public void Dispose()
        {
        }
    }

    private sealed class CapturingLogger(ConcurrentQueue<string> messages) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter) => messages.Enqueue(formatter(state, exception));
    }

    private sealed class KnownHasher : IPasswordCredentialHasher
    {
        public string Hash(User user, string password) => KnownPasswordHash;
    }

    private sealed class FailingHasher : IPasswordCredentialHasher
    {
        public string Hash(User user, string password) => throw new InvalidOperationException("Private exception marker");
    }

    private sealed class FailingContextFactory : IDbContextFactory<ChangeFlowDbContext>
    {
        public ChangeFlowDbContext CreateDbContext() => throw new InvalidOperationException("Private exception marker");
    }
    [Theory]
    [InlineData("hashing")]
    [InlineData("persistence")]
    public async Task Unexpected_failures_log_only_safe_diagnostics(string stage)
    {
        var provider = new CapturingLoggerProvider();
        using var baseFactory = new ChangeFlowApiFactory();
        using var factory = baseFactory.WithWebHostBuilder(builder => builder.ConfigureLogging(logging =>
        {
            logging.ClearProviders();
            logging.AddProvider(provider);
        }).ConfigureServices(services =>
        {
            if (stage == "hashing")
            {
                services.RemoveAll<IPasswordCredentialHasher>();
                services.AddSingleton<IPasswordCredentialHasher, FailingHasher>();
            }
            else
            {
                services.RemoveAll<IUserProvisioningStore>();
                services.AddScoped<IUserProvisioningStore>(_ => new EfUserProvisioningStore(new FailingContextFactory()));
            }
        }));
        using var client = factory.CreateClient();
        var password = TestSecretFactory.CreateValidPassword();
        const string correlation = "provision-diagnostics-test";
        client.DefaultRequestHeaders.Add(CorrelationMiddleware.HeaderName, correlation);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Setup", baseFactory.SetupCode);
        using var response = await client.PostAsJsonAsync("/users", new
        {
            displayName = "Private name marker",
            loginIdentifier = "private.identifier",
            password,
            roles = new[] { "Requester" }
        });

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        var payload = await response.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        Assert.Equal("unexpected_failure", payload.GetProperty("code").GetString());
        Assert.Equal(correlation, payload.GetProperty("traceId").GetString());
        var logs = string.Join(Environment.NewLine, provider.Messages);
        Assert.Contains($"at {stage} with InvalidOperationException; correlation {correlation}", logs,
            StringComparison.Ordinal);
        foreach (var sensitive in new[] { "Private name marker", "private.identifier", "Private exception marker",
                     password, baseFactory.SetupCode })
        {
            Assert.False(logs.Contains(sensitive, StringComparison.Ordinal));
            Assert.False(payload.GetRawText().Contains(sensitive, StringComparison.Ordinal));
        }
        Assert.DoesNotContain("InvalidOperationException", payload.GetRawText(), StringComparison.Ordinal);
    }
}
