using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text;
using ChangeFlow.Core.Application.Users.CreateUser;
using ChangeFlow.Core.Domain.Users;
using ChangeFlow.Infra.DataAccess.Common;
using ChangeFlow.Tests.Infrastructure;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.EntityFrameworkCore;

namespace ChangeFlow.Tests.Users.Contract;

public sealed class CreateUserErrorContractTests
{
    [Theory]
    [InlineData("0")]
    [InlineData("1")]
    [InlineData("Requester, Reviewer")]
    [InlineData("requester")]
    public async Task Only_exact_role_names_are_allowed(string role)
    {
        using var factory = new ChangeFlowApiFactory();
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Setup", factory.SetupCode);
        using var response = await client.PostAsJsonAsync("/users", new
        {
            displayName = "Test Account",
            loginIdentifier = "rejected.role",
            password = TestSecretFactory.CreateValidPassword(),
            roles = new[] { role }
        });

        await AssertProblem(response, HttpStatusCode.BadRequest, "validation_failed");
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(problem.GetProperty("errors").TryGetProperty("roles", out _));
        await AssertDatabaseEmptyAsync(factory);
    }

    [Theory]
    [InlineData("{\"displayName\":\"private-body-marker\"")]
    [InlineData("{\"displayName\":{},\"loginIdentifier\":\"private-body-marker\"}")]
    [InlineData("{\"displayName\":\"Test\",\"loginIdentifier\":\"test.login\",\"roles\":[\"Requester\"],\"extra\":\"private-body-marker\"}")]
    public async Task Invalid_body_returns_sanitized_validation_problem_without_writing(string body)
    {
        using var factory = new ChangeFlowApiFactory();
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Setup", factory.SetupCode);
        using var content = new StringContent(body, Encoding.UTF8, "application/json");
        using var response = await client.PostAsync("/users", content);
        await AssertProblem(response, HttpStatusCode.BadRequest, "validation_failed");
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(problem.GetProperty("errors").TryGetProperty("body", out _));
        Assert.DoesNotContain("private-body-marker", problem.GetRawText(), StringComparison.Ordinal);
        await AssertDatabaseEmptyAsync(factory);
    }

    private static async Task AssertDatabaseEmptyAsync(ChangeFlowApiFactory factory)
    {
        var contextFactory = factory.Services.GetRequiredService<IDbContextFactory<ChangeFlowDbContext>>();
        await using var context = await contextFactory.CreateDbContextAsync();
        await context.Database.OpenConnectionAsync();
        await using var command = context.Database.GetDbConnection().CreateCommand();
        command.CommandText = """
            SELECT (SELECT COUNT(*) FROM users)
                + (SELECT COUNT(*) FROM user_roles)
                + (SELECT COUNT(*) FROM user_credentials)
            """;
        Assert.Equal(0L, Convert.ToInt64(await command.ExecuteScalarAsync()));
    }

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
