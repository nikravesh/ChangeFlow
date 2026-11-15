using System.Security.Cryptography;
using System.Text;
using ChangeFlow.Core.Domain.Users;
using ChangeFlow.Infra.DataAccess.Users.Security;
using ChangeFlow.Tests.Infrastructure;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;

namespace ChangeFlow.Tests.Users.Infrastructure;

public sealed class UserSecurityAdapterTests
{
    [Fact]
    public void Password_hasher_returns_salted_versioned_hash_instead_of_plaintext()
    {
        var adapter = new IdentityPasswordCredentialHasher();
        var user = User.Create("Alice", "alice", [UserRole.Requester]);
        var password = TestSecretFactory.CreateValidPassword();

        var firstHash = adapter.Hash(user, password);
        var secondHash = adapter.Hash(user, password);

        Assert.NotEqual(password, firstHash);
        Assert.NotEqual(firstHash, secondHash);
        var verifier = new PasswordHasher<User>();
        Assert.NotEqual(
            PasswordVerificationResult.Failed,
            verifier.VerifyHashedPassword(user, firstHash, password));
    }

    [Fact]
    public void Setup_code_is_reusable_until_rotation_then_old_value_is_rejected()
    {
        var configuration = new ConfigurationManager();
        var firstCode = TestSecretFactory.CreateSetupCode();
        var secondCode = TestSecretFactory.CreateSetupCode();
        configuration[ConfigurationSetupCodeValidator.ConfigurationKey] = Digest(firstCode);
        var validator = new ConfigurationSetupCodeValidator(configuration);

        Assert.True(validator.IsValid(firstCode));
        Assert.True(validator.IsValid(firstCode));

        configuration[ConfigurationSetupCodeValidator.ConfigurationKey] = Digest(secondCode);

        Assert.False(validator.IsValid(firstCode));
        Assert.True(validator.IsValid(secondCode));
    }

    private static string Digest(string value) => Convert.ToHexString(
        SHA256.HashData(Encoding.UTF8.GetBytes(value)));
}
