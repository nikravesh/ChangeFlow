using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json;
using ChangeFlow.Core.Domain.Users;
using ChangeFlow.Tests.Infrastructure;
using ChangeFlow.Endpoints.Api.ChangeRequests;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace ChangeFlow.Tests.ChangeRequests.Infrastructure;

public sealed class ChangeRequestApiFixture : IAsyncLifetime
{
    private readonly ChangeFlowApiFactory databaseFactory = new();

    public const string Issuer = "https://changeflow-issuer.example";
    public const string Audience = "changeflow-api-tests";

    public SymmetricSecurityKey SigningKey { get; } =
        new(RandomNumberGenerator.GetBytes(32));

    public TestClock Clock { get; } = new();
    public WebApplicationFactory<Program> Factory { get; private set; }
    public UserId Requester { get; private set; }
    public UserId Reviewer { get; private set; }
    public UserId SecondReviewer { get; private set; }
    public UserId DualRoleUser { get; private set; }

    public ChangeRequestApiFixture()
    {
        Factory = CreateFactory();
    }

    public async Task RestartAsync()
    {
        await Factory.DisposeAsync();
        Factory = CreateFactory();
    }

    private WebApplicationFactory<Program> CreateFactory() =>
        databaseFactory.WithWebHostBuilder(builder =>
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<TimeProvider>();
                services.AddSingleton<TimeProvider>(Clock);
                services.PostConfigure<JwtBearerOptions>(ChangeRequestRouteGroup.Scheme, options =>
                {
                    options.TokenValidationParameters.ValidIssuer = Issuer;
                    options.TokenValidationParameters.ValidAudience = Audience;
                    options.TokenValidationParameters.IssuerSigningKey = SigningKey;
                });
            }));

    public async Task InitializeAsync()
    {
        using var client = Factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Setup", databaseFactory.SetupCode);

        Requester = await ProvisionAsync(client, "workflow.requester", UserRole.Requester);
        Reviewer = await ProvisionAsync(client, "workflow.reviewer", UserRole.Reviewer);
        SecondReviewer = await ProvisionAsync(client, "workflow.reviewer2", UserRole.Reviewer);
        DualRoleUser = await ProvisionAsync(
            client, "workflow.dual", UserRole.Requester, UserRole.Reviewer);
    }

    public string CreateAccessToken(UserId userId)
    {
        var now = Clock.GetUtcNow().UtcDateTime;
        return new JsonWebTokenHandler().CreateToken(new SecurityTokenDescriptor
        {
            Issuer = Issuer,
            Audience = Audience,
            Claims = new Dictionary<string, object> { ["sub"] = userId.ToString() },
            IssuedAt = now,
            NotBefore = now,
            Expires = now.AddMinutes(15),
            SigningCredentials = new SigningCredentials(SigningKey, SecurityAlgorithms.HmacSha256)
        });
    }

    public HttpClient CreateAuthenticatedClient(UserId userId)
    {
        var client = Factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", CreateAccessToken(userId));
        return client;
    }

    public Task DisposeAsync() => databaseFactory.DisposeAsync().AsTask();

    public async Task<UserId> ProvisionAsync(string loginIdentifier, UserRole role)
    {
        using var client = Factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Setup", databaseFactory.SetupCode);
        return await ProvisionAsync(client, loginIdentifier, role);
    }

    private static async Task<UserId> ProvisionAsync(
        HttpClient client, string loginIdentifier, params UserRole[] roles)
    {
        using var response = await client.PostAsJsonAsync("/users", new
        {
            displayName = loginIdentifier,
            loginIdentifier,
            password = TestSecretFactory.CreateValidPassword(),
            roles = roles.Select(role => role.ToString()).ToArray()
        });
        response.EnsureSuccessStatusCode();

        var payload = await response.Content.ReadFromJsonAsync<JsonElement>();
        return new UserId(payload.GetProperty("id").GetGuid());
    }

    public sealed class TestClock : TimeProvider
    {
        private long utcTicks = DateTimeOffset.UtcNow.UtcTicks;

        public override DateTimeOffset GetUtcNow() =>
            new(Interlocked.Read(ref utcTicks), TimeSpan.Zero);

        public void Advance(TimeSpan duration) =>
            Interlocked.Add(ref utcTicks, duration.Ticks);
    }
}
