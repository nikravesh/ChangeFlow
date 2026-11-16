using System.Text.Json.Serialization;

namespace ChangeFlow.Endpoints.Api.Users.CreateUser;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record CreateUserRequest(
    string? DisplayName,
    string? LoginIdentifier,
    string? Password,
    IReadOnlyCollection<string>? Roles);
