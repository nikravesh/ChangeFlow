using System.Net;
using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using ChangeFlow.Core.Application.ChangeRequests;
using ChangeFlow.Core.Domain.Users;
using ChangeFlow.Endpoints.Api.ChangeRequests;
using ChangeFlow.Endpoints.Api.Common.Observability;
using ChangeFlow.Tests.ChangeRequests.Infrastructure;
using ChangeFlow.Tests.Infrastructure;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace ChangeFlow.Tests.ChangeRequests.Contract;

public sealed class ChangeRequestAuthenticationTests(ChangeRequestApiFixture fixture)
    : IClassFixture<ChangeRequestApiFixture>
{
    [Fact]
    public async Task Production_route_accepts_a_token_using_discovery_and_jwks()
    {
        const string authority = "https://discovery-issuer.example";
        const string audience = "discovery-workflow-api";
        const string keyId = "discovery-test-key";
        using var rsa = RSA.Create(2048);
        var key = new RsaSecurityKey(rsa) { KeyId = keyId };
        var publicKey = rsa.ExportParameters(false);
        var issuerRequests = new ConcurrentQueue<string>();
        var issuerBuilder = WebApplication.CreateBuilder();
        issuerBuilder.WebHost.UseTestServer();
        await using var issuer = issuerBuilder.Build();
        issuer.Run(async context =>
        {
            issuerRequests.Enqueue(context.Request.Path.Value!);
            switch (context.Request.Path.Value)
            {
                case "/.well-known/openid-configuration":
                    await context.Response.WriteAsJsonAsync(new { issuer = authority, jwks_uri = $"{authority}/keys" });
                    break;
                case "/keys":
                    await context.Response.WriteAsJsonAsync(new
                    {
                        keys = new[]
                        {
                            new { kty = "RSA", use = "sig", alg = "RS256", kid = keyId,
                                n = Base64UrlEncoder.Encode(publicKey.Modulus!),
                                e = Base64UrlEncoder.Encode(publicKey.Exponent!) }
                        }
                    });
                    break;
                default:
                    context.Response.StatusCode = StatusCodes.Status404NotFound;
                    break;
            }
        });
        await issuer.StartAsync();
        using var database = new ChangeFlowApiFactory();
        await using var factory = database.WithWebHostBuilder(builder =>
        {
            builder.ConfigureAppConfiguration((_, configuration) =>
                configuration.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["ChangeRequestsAuthentication:Authority"] = authority,
                    ["ChangeRequestsAuthentication:Audience"] = audience
                }));
            builder.ConfigureServices(services =>
                services.Configure<JwtBearerOptions>(ChangeRequestRouteGroup.Scheme,
                    options => options.BackchannelHttpHandler = issuer.GetTestServer().CreateHandler()));
        });
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new("Setup", database.SetupCode);
        using var provisioned = await client.PostAsJsonAsync("/users", new
        {
            displayName = "Discovery Requester",
            loginIdentifier = "discovery.requester",
            password = TestSecretFactory.CreateValidPassword(),
            roles = new[] { "Requester" }
        });
        Assert.Equal(HttpStatusCode.Created, provisioned.StatusCode);
        var user = await provisioned.Content.ReadFromJsonAsync<JsonElement>();
        var id = user.GetProperty("id").GetGuid();
        var now = DateTime.UtcNow;
        var token = new JsonWebTokenHandler().CreateToken(new SecurityTokenDescriptor
        {
            Issuer = authority,
            Audience = audience,
            Claims = new Dictionary<string, object> { ["sub"] = id.ToString() },
            NotBefore = now.AddSeconds(-5),
            Expires = now.AddMinutes(5),
            SigningCredentials = new SigningCredentials(key, SecurityAlgorithms.RsaSha256)
        });
        client.DefaultRequestHeaders.Authorization = new("Bearer", token);
        using var response = await client.PostAsJsonAsync("/change-requests",
            new { title = "Discovery", description = "Detail", reason = "Need" });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var draft = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(id, draft.GetProperty("requesterId").GetGuid());
        Assert.Contains("/.well-known/openid-configuration", issuerRequests);
        Assert.Contains("/keys", issuerRequests);
    }
    [Theory]
    [InlineData("missing", HttpStatusCode.Unauthorized, "authentication_required")]
    [InlineData("unsigned", HttpStatusCode.Unauthorized, "authentication_required")]
    [InlineData("wrong-signature", HttpStatusCode.Unauthorized, "authentication_required")]
    [InlineData("wrong-issuer", HttpStatusCode.Unauthorized, "authentication_required")]
    [InlineData("wrong-audience", HttpStatusCode.Unauthorized, "authentication_required")]
    [InlineData("expired", HttpStatusCode.Unauthorized, "authentication_required")]
    [InlineData("unknown-account", HttpStatusCode.Unauthorized, "authentication_required")]
    [InlineData("invalid-subject", HttpStatusCode.Unauthorized, "authentication_required")]
    [InlineData("claimed-role", HttpStatusCode.Forbidden, "forbidden")]
    public async Task Protected_route_rejects_invalid_identity_or_missing_local_role(
        string scenario, HttpStatusCode expectedStatus, string expectedCode)
    {
        await using var factory = CreateFactory();
        using var client = factory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, "/change-requests/test-access");
        if (scenario != "missing")
        {
            request.Headers.Authorization = new("Bearer", CreateToken(scenario));
        }

        using var response = await client.SendAsync(request);

        Assert.Equal(expectedStatus, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        var payload = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(expectedCode, payload.GetProperty("code").GetString());
        Assert.Equal(response.Headers.GetValues(CorrelationMiddleware.HeaderName).Single(),
            payload.GetProperty("traceId").GetString());
        if (expectedStatus == HttpStatusCode.Unauthorized)
        {
            Assert.Contains(response.Headers.WwwAuthenticate, value =>
                value.Scheme == "Bearer" && value.Parameter is null);
        }
    }

    [Fact]
    public async Task Valid_bearer_uses_local_requester_role()
    {
        await using var factory = CreateFactory();
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", fixture.CreateAccessToken(fixture.Requester));

        using var response = await client.GetAsync("/change-requests/test-access");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    private Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactory<Program> CreateFactory() =>
        fixture.Factory.WithWebHostBuilder(builder => builder.ConfigureServices(services =>
        {
            services.AddTransient<IStartupFilter, TestRoute>();
            services.PostConfigure<JwtBearerOptions>(ChangeRequestRouteGroup.Scheme, options =>
            {
                options.TokenValidationParameters.ValidIssuer = ChangeRequestApiFixture.Issuer;
                options.TokenValidationParameters.ValidAudience = ChangeRequestApiFixture.Audience;
                options.TokenValidationParameters.IssuerSigningKey = fixture.SigningKey;
            });
        }));

    private string CreateToken(string scenario)
    {
        var now = fixture.Clock.GetUtcNow().UtcDateTime;
        var subject = scenario switch
        {
            "unknown-account" => Guid.NewGuid().ToString(),
            "invalid-subject" => "invalid",
            "claimed-role" => fixture.Reviewer.ToString(),
            _ => fixture.Requester.ToString()
        };
        var key = scenario == "wrong-signature"
            ? new SymmetricSecurityKey(System.Security.Cryptography.RandomNumberGenerator.GetBytes(32))
            : fixture.SigningKey;
        return new JsonWebTokenHandler().CreateToken(new SecurityTokenDescriptor
        {
            Issuer = scenario == "wrong-issuer" ? "https://other.example" : ChangeRequestApiFixture.Issuer,
            Audience = scenario == "wrong-audience" ? "other-api" : ChangeRequestApiFixture.Audience,
            Claims = new Dictionary<string, object> { ["sub"] = subject, ["role"] = "Requester" },
            IssuedAt = now.AddMinutes(-10),
            NotBefore = now.AddMinutes(-10),
            Expires = scenario == "expired" ? now.AddMinutes(-5) : now.AddMinutes(15),
            SigningCredentials = scenario == "unsigned"
                ? null : new SigningCredentials(key, SecurityAlgorithms.HmacSha256)
        });
    }

    private sealed class TestRoute : IStartupFilter
    {
        public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) => app =>
        {
            app.UseMiddleware<CorrelationMiddleware>();
            app.UseRouting();
            app.UseAuthorization();
            app.UseEndpoints(endpoints => endpoints.MapChangeRequests().MapGet(
                "/test-access", async (HttpContext context, IChangeRequestTransaction transaction) =>
                {
                    var failure = await transaction.ReadAsync(async (session, cancellationToken) =>
                    {
                        var roles = await session.ReadRolesAsync(
                            ChangeRequestRouteGroup.GetUserId(context.User), cancellationToken);
                        return ChangeRequestAccess.RequireRole(roles, UserRole.Requester);
                    }, context.RequestAborted);
                    return failure is { } error
                        ? ChangeRequestRouteGroup.ToProblem(context, error)
                        : Results.NoContent();
                }));
            next(app);
        };
    }
}
