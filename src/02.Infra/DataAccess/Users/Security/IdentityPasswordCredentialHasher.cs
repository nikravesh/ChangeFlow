using ChangeFlow.Core.Application.Users.CreateUser;
using ChangeFlow.Core.Domain.Users;
using Microsoft.AspNetCore.Identity;

namespace ChangeFlow.Infra.DataAccess.Users.Security;

public sealed class IdentityPasswordCredentialHasher : IPasswordCredentialHasher
{
    private readonly PasswordHasher<User> hasher = new();

    public string Hash(User user, string password) => hasher.HashPassword(user, password);
}
