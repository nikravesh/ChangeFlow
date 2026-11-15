using ChangeFlow.Core.Domain.Users;

namespace ChangeFlow.Core.Application.Users.CreateUser;

public sealed record CreateUserSuccess(
    UserId Id,
    string DisplayName,
    string LoginIdentifier,
    IReadOnlyCollection<UserRole> Roles) : CreateUserResult;
