using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using ChangeFlow.Core.Application.Users.CreateUser;
using ChangeFlow.Core.Domain.Users;
using ChangeFlow.Tests.Infrastructure;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace ChangeFlow.Tests.Users.Contract;

public sealed class CreateUserErrorContractTests
{
    [Fact]
    public async Task Validation_failure_returns_400_problem_details()
    {
        using var factory = new ChangeFlowApiFactory();
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Setup", factory.SetupCode);

        using var response = await client.PostAsJsonAsync("/users", new
        {
            displayName = " ",
            loginIdentifier = " ",
            password = "weak",
            roles = new[] { "Unknown" }
        });

        await AssertProblem(response, HttpStatusCode.BadRequest, "validation_failed");
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.True(document.RootElement.TryGetProperty("errors", out _));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Missing_and_invalid_setup_code_have_identical_401_contract(bool includeInvalid)
    {
        using var factory = new ChangeFlowApiFactory();
        using var client = factory.CreateClient();
        if (includeInvalid)
        {
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Setup", "invalid");
        }

        using var response = await client.PostAsJsonAsync("/users", ValidRequest("unauthorized" + includeInvalid));

        await AssertProblem(response, HttpStatusCode.Unauthorized, "invalid_setup_code");
        Assert.Equal("Setup", response.Headers.WwwAuthenticate.Single().Scheme);
    }

    [Fact]
    public async Task Duplicate_returns_409_problem_details()
    {
        using var factory = new ChangeFlowApiFactory();
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Setup", factory.SetupCode);
        using var first = await client.PostAsJsonAsync("/users", ValidRequest("duplicate"));
        Assert.Equal(HttpStatusCode.Created, first.StatusCode);

        using var response = await client.PostAsJsonAsync("/users", ValidRequest("DUPLICATE"));

        await AssertProblem(response, HttpStatusCode.Conflict, "login_identifier_conflict");
    }

    [Fact]
    public async Task Unexpected_failure_returns_sanitized_500_problem_details()
    {
        using var baseFactory = new ChangeFlowApiFactory();
        using var factory = baseFactory.WithWebHostBuilder(builder => builder.ConfigureServices(services =>
        {
            services.RemoveAll<IUserProvisioningStore>();
            services.AddSingleton<IUserProvisioningStore, FailingStore>();
        }));
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Setup", baseFactory.SetupCode);

        using var response = await client.PostAsJsonAsync("/users", ValidRequest("failure"));

        await AssertProblem(response, HttpStatusCode.InternalServerError, "unexpected_failure");
        var payload = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain("stack", payload, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("simulated", payload, StringComparison.OrdinalIgnoreCase);
        Assert.False(payload.Contains(baseFactory.SetupCode, StringComparison.Ordinal));
    }

    private static async Task AssertProblem(HttpResponseMessage response, HttpStatusCode status, string code)
    {
        Assert.Equal(status, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var root = document.RootElement;
        Assert.Equal((int)status, root.GetProperty("status").GetInt32());
        Assert.Equal(code, root.GetProperty("code").GetString());
        Assert.False(string.IsNullOrWhiteSpace(root.GetProperty("traceId").GetString()));
        Assert.False(string.IsNullOrWhiteSpace(root.GetProperty("type").GetString()));
    }

    private static object ValidRequest(string identifier) => new
    {
        displayName = "Alice",
        loginIdentifier = identifier,
        password = TestSecretFactory.CreateValidPassword(),
        roles = new[] { "Requester" }
    };

    private sealed class FailingStore : IUserProvisioningStore
    {
        public Task<CreateUserFailure?> ProvisionAsync(
            User user,
            string passwordHash,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<CreateUserFailure?>(new UnexpectedFailure());
    }
}
