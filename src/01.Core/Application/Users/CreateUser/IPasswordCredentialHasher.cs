using ChangeFlow.Core.Domain.Users;

namespace ChangeFlow.Core.Application.Users.CreateUser;

public interface IPasswordCredentialHasher
{
    string Hash(User user, string password);
}
