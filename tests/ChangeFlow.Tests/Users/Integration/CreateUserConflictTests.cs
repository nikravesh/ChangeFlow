using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using ChangeFlow.Tests.Infrastructure;

namespace ChangeFlow.Tests.Users.Integration;

public sealed class CreateUserConflictTests
{
    [Fact]
    public async Task Serial_case_insensitive_duplicate_returns_conflict()
    {
        using var factory = new ChangeFlowApiFactory();
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Setup", factory.SetupCode);

        using var first = await client.PostAsJsonAsync("/users", Request("Alice.User"));
        using var duplicate = await client.PostAsJsonAsync("/users", Request("  alice.user  "));

        Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, duplicate.StatusCode);
        using var problem = JsonDocument.Parse(await duplicate.Content.ReadAsStringAsync());
        Assert.Equal("login_identifier_conflict", problem.RootElement.GetProperty("code").GetString());
    }

    [Fact]
    public async Task Concurrent_equivalent_identifiers_produce_one_created_and_one_conflict()
    {
        using var factory = new ChangeFlowApiFactory();
        using var firstClient = factory.CreateClient();
        using var secondClient = factory.CreateClient();
        firstClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Setup", factory.SetupCode);
        secondClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Setup", factory.SetupCode);

        var responses = await Task.WhenAll(
            firstClient.PostAsJsonAsync("/users", Request("Concurrent.User")),
            secondClient.PostAsJsonAsync("/users", Request("concurrent.user")));

        try
        {
            Assert.Equal(
                [HttpStatusCode.Created, HttpStatusCode.Conflict],
                responses.Select(response => response.StatusCode).Order());
            var payloads = await Task.WhenAll(responses.Select(response => response.Content.ReadAsStringAsync()));
            Assert.DoesNotContain(payloads, payload =>
                payload.Contains("SQLITE_BUSY", StringComparison.OrdinalIgnoreCase)
                || payload.Contains("SQLITE_LOCKED", StringComparison.OrdinalIgnoreCase));
        }
        finally
        {
            foreach (var response in responses)
            {
                response.Dispose();
            }
        }
    }

    private static object Request(string identifier) => new
    {
        displayName = "Alice",
        loginIdentifier = identifier,
        password = TestSecretFactory.CreateValidPassword(),
        roles = new[] { "Requester" }
    };
}
