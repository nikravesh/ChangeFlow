using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using ChangeFlow.Tests.Infrastructure;

namespace ChangeFlow.Tests.Users.Contract;

public sealed class CreateUserSuccessContractTests
{
    [Fact]
    public async Task Post_users_returns_only_approved_201_projection_and_reuses_setup_code()
    {
        using var factory = new ChangeFlowApiFactory();
        using var client = factory.CreateClient();
        var firstPassword = TestSecretFactory.CreateValidPassword();
        var secondPassword = TestSecretFactory.CreateValidPassword();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Setup", factory.SetupCode);

        using var firstResponse = await client.PostAsJsonAsync("/users", new
        {
            displayName = " Alice ",
            loginIdentifier = " Alice.User ",
            password = firstPassword,
            roles = new[] { "Requester", "Reviewer", "Requester" }
        });
        using var secondResponse = await client.PostAsJsonAsync("/users", new
        {
            displayName = "Bob",
            loginIdentifier = "bob.user",
            password = secondPassword,
            roles = new[] { "Reviewer" }
        });

        Assert.Equal(HttpStatusCode.Created, firstResponse.StatusCode);
        Assert.Equal(HttpStatusCode.Created, secondResponse.StatusCode);
        Assert.Null(firstResponse.Headers.Location);

        using var document = JsonDocument.Parse(await firstResponse.Content.ReadAsStringAsync());
        var root = document.RootElement;
        Assert.Equal(
            ["displayName", "id", "loginIdentifier", "roles"],
            root.EnumerateObject().Select(property => property.Name).Order());
        Assert.NotEqual(Guid.Empty, root.GetProperty("id").GetGuid());
        Assert.Equal("Alice", root.GetProperty("displayName").GetString());
        Assert.Equal("Alice.User", root.GetProperty("loginIdentifier").GetString());
        Assert.Equal(
            ["Requester", "Reviewer"],
            root.GetProperty("roles").EnumerateArray().Select(role => role.GetString()).Order());

        var payload = root.GetRawText();
        Assert.DoesNotContain("password", payload, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("hash", payload, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("normalized", payload, StringComparison.OrdinalIgnoreCase);
        Assert.False(payload.Contains(factory.SetupCode, StringComparison.Ordinal));
    }
}
