namespace ChangeFlow.Endpoints.Api.Users.CreateUser;

public sealed record CreateUserRequest(
    string? DisplayName,
    string? LoginIdentifier,
    string? Password,
    IReadOnlyCollection<string>? Roles);
