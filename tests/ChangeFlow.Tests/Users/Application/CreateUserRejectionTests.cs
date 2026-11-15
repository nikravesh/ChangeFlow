using ChangeFlow.Core.Application.Users.CreateUser;
using ChangeFlow.Core.Domain.Users;

namespace ChangeFlow.Tests.Users.Application;

public sealed class CreateUserRejectionTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("invalid")]
    public async Task Invalid_setup_code_is_rejected_before_hashing_or_persistence(string? setupCode)
    {
        var hasher = new TrackingHasher();
        var store = new TrackingStore();
        var useCase = new CreateUserUseCase(new RejectingSetupCode(), hasher, store);

        var result = await useCase.ExecuteAsync(ValidCommand(setupCode));

        Assert.IsType<InvalidSetupCode>(result);
        Assert.False(hasher.WasCalled);
        Assert.False(store.WasCalled);
    }

    [Fact]
    public async Task All_password_rule_failures_are_returned_without_mutation()
    {
        var hasher = new TrackingHasher();
        var store = new TrackingStore();
        var useCase = new CreateUserUseCase(new AcceptingSetupCode(), hasher, store);
        var command = ValidCommand() with { Password = "abc" };

        var failure = Assert.IsType<ValidationFailure>(await useCase.ExecuteAsync(command));

        Assert.Equal(4, failure.Errors["password"].Length);
        Assert.Contains(failure.Errors["password"], error => error.Contains("8", StringComparison.Ordinal));
        Assert.Contains(failure.Errors["password"], error => error.Contains("uppercase", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(failure.Errors["password"], error => error.Contains("symbol", StringComparison.OrdinalIgnoreCase));
        Assert.False(hasher.WasCalled);
        Assert.False(store.WasCalled);
    }

    [Theory]
    [InlineData("Aa1!", "8")]
    [InlineData("valid1!password", "uppercase")]
    [InlineData("VALID1!PASSWORD", "lowercase")]
    [InlineData("Valid!Password", "number")]
    [InlineData("Valid1Password", "symbol")]
    public async Task Each_password_policy_rule_is_reported(string password, string expectedRule)
    {
        var hasher = new TrackingHasher();
        var store = new TrackingStore();
        var useCase = new CreateUserUseCase(new AcceptingSetupCode(), hasher, store);

        var failure = Assert.IsType<ValidationFailure>(
            await useCase.ExecuteAsync(ValidCommand() with { Password = password }));

        Assert.Contains(
            failure.Errors["password"],
            error => error.Contains(expectedRule, StringComparison.OrdinalIgnoreCase));
        Assert.False(hasher.WasCalled);
        Assert.False(store.WasCalled);
    }

    [Fact]
    public async Task Missing_fields_and_unknown_role_are_collected_before_mutation()
    {
        var hasher = new TrackingHasher();
        var store = new TrackingStore();
        var useCase = new CreateUserUseCase(new AcceptingSetupCode(), hasher, store);
        var command = new CreateUserCommand(
            "valid",
            " ",
            " ",
            null!,
            [(UserRole)999]);

        var failure = Assert.IsType<ValidationFailure>(await useCase.ExecuteAsync(command));

        Assert.Equal(["displayName", "loginIdentifier", "password", "roles"], failure.Errors.Keys.Order());
        Assert.False(hasher.WasCalled);
        Assert.False(store.WasCalled);
    }

    private static CreateUserCommand ValidCommand(string? setupCode = "valid") => new(
        setupCode,
        "Alice",
        "alice",
        "Valid1!Password",
        [UserRole.Requester]);

    private sealed class AcceptingSetupCode : ISetupCodeValidator
    {
        public bool IsValid(string? candidate) => true;
    }

    private sealed class RejectingSetupCode : ISetupCodeValidator
    {
        public bool IsValid(string? candidate) => false;
    }

    private sealed class TrackingHasher : IPasswordCredentialHasher
    {
        public bool WasCalled { get; private set; }

        public string Hash(User user, string password)
        {
            WasCalled = true;
            return "opaque-hash";
        }
    }

    private sealed class TrackingStore : IUserProvisioningStore
    {
        public bool WasCalled { get; private set; }

        public Task<CreateUserFailure?> ProvisionAsync(
            User user,
            string passwordHash,
            CancellationToken cancellationToken = default)
        {
            WasCalled = true;
            return Task.FromResult<CreateUserFailure?>(null);
        }
    }
}
