using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using ChangeFlow.Endpoints.Api.Common.Observability;
using ChangeFlow.Tests.ChangeRequests.Infrastructure;
using ChangeFlow.Tests.Infrastructure;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using static ChangeFlow.Tests.ChangeRequests.Contract.ReviewWorkflowContractTests;

namespace ChangeFlow.Tests.ChangeRequests.Contract;

public sealed class WorkflowBoundaryTests(ChangeRequestApiFixture fixture)
    : IClassFixture<ChangeRequestApiFixture>
{
    [Fact]
    public async Task Setup_user_provisioning_keeps_its_original_authentication_and_response()
    {
        using var factory = new ChangeFlowApiFactory();
        using var client = factory.CreateClient();
        var body = new
        {
            displayName = "Boundary User",
            loginIdentifier = "boundary.user",
            password = TestSecretFactory.CreateValidPassword(),
            roles = new[] { "Requester" }
        };
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", fixture.CreateAccessToken(fixture.Requester));
        await AssertProblemAsync(await client.PostAsJsonAsync("/users", body),
            HttpStatusCode.Unauthorized, "invalid_setup_code");

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Setup", factory.SetupCode);
        using var response = await client.PostAsJsonAsync("/users", body);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Null(response.Headers.Location);
        var payload = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(new[] { "displayName", "id", "loginIdentifier", "roles" },
            payload.EnumerateObject().Select(property => property.Name).Order());
        Assert.NotEqual(Guid.Empty, payload.GetProperty("id").GetGuid());
    }

    [Fact]
    public void Workflow_registers_only_the_seven_contracted_routes()
    {
        using var client = fixture.Factory.CreateClient();
        var routes = fixture.Factory.Services.GetRequiredService<EndpointDataSource>().Endpoints
            .OfType<RouteEndpoint>()
            .Where(endpoint => endpoint.RoutePattern.RawText?.StartsWith("/change-requests", StringComparison.Ordinal) == true)
            .SelectMany(endpoint => endpoint.Metadata.GetRequiredMetadata<HttpMethodMetadata>().HttpMethods
                .Select(method => $"{method} {endpoint.RoutePattern.RawText!.TrimEnd('/')}"))
            .Order(StringComparer.Ordinal);

        Assert.Equal(new[]
        {
            "GET /change-requests/{id}",
            "GET /change-requests/{id}/review-history",
            "POST /change-requests",
            "POST /change-requests/{id}/approve",
            "POST /change-requests/{id}/request-changes",
            "POST /change-requests/{id}/submit",
            "PUT /change-requests/{id}"
        }, routes);
    }

    [Fact]
    public async Task Correlation_is_logged_without_token_or_workflow_content()
    {
        var logger = new CaptureProvider();
        await using var factory = fixture.Factory.WithWebHostBuilder(builder => builder.ConfigureLogging(logging =>
        {
            logging.ClearProviders();
            logging.AddProvider(logger);
        }));
        using var owner = factory.CreateClient();
        using var reviewer = factory.CreateClient();
        var ownerToken = fixture.CreateAccessToken(fixture.Requester);
        var reviewerToken = fixture.CreateAccessToken(fixture.Reviewer);
        owner.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", ownerToken);
        reviewer.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", reviewerToken);
        const string title = "Synthetic confidential title marker";
        const string description = "Synthetic confidential description marker";
        const string reason = "Synthetic confidential reason marker";
        const string feedback = "Synthetic confidential reviewer feedback marker";
        const string correlation = "boundary-review-123";
        reviewer.DefaultRequestHeaders.Add(CorrelationMiddleware.HeaderName, correlation);

        using var created = await owner.PostAsJsonAsync("/change-requests", new { title, description, reason });
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var path = created.Headers.Location!.OriginalString;
        using var submitted = await owner.PostAsJsonAsync($"{path}/submit", new { expectedVersion = "1" });
        Assert.Equal(HttpStatusCode.OK, submitted.StatusCode);
        using var returned = await reviewer.PostAsJsonAsync($"{path}/request-changes",
            new { expectedVersion = "2", feedback });
        Assert.Equal(HttpStatusCode.OK, returned.StatusCode);
        using var conflict = await reviewer.PostAsJsonAsync($"{path}/approve", new { expectedVersion = "2" });
        Assert.Equal(HttpStatusCode.Conflict, conflict.StatusCode);
        Assert.Equal(correlation, Assert.Single(conflict.Headers.GetValues(CorrelationMiddleware.HeaderName)));
        var problem = await conflict.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(correlation, problem.GetProperty("traceId").GetString());
        Assert.Equal("version_conflict", problem.GetProperty("code").GetString());

        Assert.Contains(logger.Entries, entry => entry.Scope.Contains(correlation, StringComparison.Ordinal)
            && entry.Message.Contains("version_conflict", StringComparison.Ordinal));
        foreach (var entry in logger.Entries)
        {
            var logged = $"{entry.Scope} {entry.Message}";
            Assert.False(logged.Contains(ownerToken, StringComparison.Ordinal));
            Assert.False(logged.Contains(reviewerToken, StringComparison.Ordinal));
            Assert.DoesNotContain(title, logged, StringComparison.Ordinal);
            Assert.DoesNotContain(description, logged, StringComparison.Ordinal);
            Assert.DoesNotContain(reason, logged, StringComparison.Ordinal);
            Assert.DoesNotContain(feedback, logged, StringComparison.Ordinal);
        }
    }

    private sealed class CaptureProvider : ILoggerProvider, ISupportExternalScope
    {
        private IExternalScopeProvider scopeProvider = new LoggerExternalScopeProvider();
        public ConcurrentQueue<(string Message, string Scope)> Entries { get; } = new();

        public void SetScopeProvider(IExternalScopeProvider provider) => scopeProvider = provider;
        public ILogger CreateLogger(string categoryName) => new CaptureLogger(this);
        public void Dispose() { }

        private sealed class CaptureLogger(CaptureProvider provider) : ILogger
        {
            public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
            public bool IsEnabled(LogLevel logLevel) => true;

            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
                Func<TState, Exception?, string> formatter)
            {
                var scopes = new List<string>();
                provider.scopeProvider.ForEachScope((scope, entries) =>
                {
                    if (scope is IEnumerable<KeyValuePair<string, object?>> fields)
                    {
                        entries.AddRange(fields.Select(field => $"{field.Key}={field.Value}"));
                    }
                    else if (scope is not null)
                    {
                        entries.Add(scope.ToString()!);
                    }
                }, scopes);
                provider.Entries.Enqueue(($"{formatter(state, exception)} {exception}", string.Join(" ", scopes)));
            }
        }
    }
}
