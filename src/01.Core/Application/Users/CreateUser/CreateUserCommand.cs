using ChangeFlow.Core.Domain.Users;

namespace ChangeFlow.Core.Application.Users.CreateUser;

public sealed record CreateUserCommand(
    string? SetupCode,
    string? DisplayName,
    string? LoginIdentifier,
    string? Password,
    IReadOnlyCollection<UserRole>? Roles);
