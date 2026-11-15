using ChangeFlow.Core.Domain.Users;

namespace ChangeFlow.Core.Application.Users.CreateUser;

public interface IUserProvisioningStore
{
    Task<CreateUserFailure?> ProvisionAsync(
        User user,
        string passwordHash,
        CancellationToken cancellationToken = default);
}
