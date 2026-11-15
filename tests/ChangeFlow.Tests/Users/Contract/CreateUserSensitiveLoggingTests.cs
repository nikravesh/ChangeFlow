using System.Collections.Concurrent;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using ChangeFlow.Core.Application.Users.CreateUser;
using ChangeFlow.Core.Domain.Users;
using ChangeFlow.Tests.Infrastructure;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;

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
}
