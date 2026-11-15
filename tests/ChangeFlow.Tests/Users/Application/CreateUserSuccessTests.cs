using ChangeFlow.Core.Application.Users.CreateUser;
using ChangeFlow.Core.Domain.Users;
using ChangeFlow.Tests.Infrastructure;

namespace ChangeFlow.Tests.Users.Application;

public sealed class CreateUserSuccessTests
{
    public static TheoryData<UserRole[]> RoleCombinations => new()
    {
        new[] { UserRole.Requester },
        new[] { UserRole.Reviewer },
        new[] { UserRole.Requester, UserRole.Reviewer }
    };

    [Theory]
    [MemberData(nameof(RoleCombinations))]
    public async Task Valid_command_returns_public_projection_and_persists_same_user(
        UserRole[] roles)
    {
        var store = new CapturingStore();
        var useCase = new CreateUserUseCase(
            new AlwaysValidSetupCode(),
            new DeterministicHasher(),
            store);
        var command = new CreateUserCommand(
            TestSecretFactory.CreateSetupCode(),
            " Alice ",
            " alice ",
            TestSecretFactory.CreateValidPassword(),
            roles);

        var result = await useCase.ExecuteAsync(command);
        var success = Assert.IsType<CreateUserSuccess>(result);

        Assert.NotNull(store.User);
        Assert.Equal(store.User.Id, success.Id);
        Assert.Equal("Alice", success.DisplayName);
        Assert.Equal("alice", success.LoginIdentifier);
        Assert.Equal(roles.Order(), success.Roles.Order());
        Assert.Equal("opaque-password-hash", store.PasswordHash);
        Assert.DoesNotContain(
            success.GetType().GetProperties(),
            property => property.Name.Contains("Password", StringComparison.OrdinalIgnoreCase)
                || property.Name.Contains("Setup", StringComparison.OrdinalIgnoreCase));
    }

    private sealed class AlwaysValidSetupCode : ISetupCodeValidator
    {
        public bool IsValid(string? candidate) => true;
    }

    private sealed class DeterministicHasher : IPasswordCredentialHasher
    {
        public string Hash(User user, string password) => "opaque-password-hash";
    }

    private sealed class CapturingStore : IUserProvisioningStore
    {
        public User? User { get; private set; }
        public string? PasswordHash { get; private set; }

        public Task<CreateUserFailure?> ProvisionAsync(
            User user,
            string passwordHash,
            CancellationToken cancellationToken = default)
        {
            User = user;
            PasswordHash = passwordHash;
            return Task.FromResult<CreateUserFailure?>(null);
        }
    }
}
