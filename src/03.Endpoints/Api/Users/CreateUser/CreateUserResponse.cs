namespace ChangeFlow.Endpoints.Api.Users.CreateUser;

public sealed record CreateUserResponse(
    Guid Id,
    string DisplayName,
    string LoginIdentifier,
    IReadOnlyCollection<string> Roles);
