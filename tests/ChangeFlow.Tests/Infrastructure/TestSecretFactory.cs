using System.Security.Cryptography;

namespace ChangeFlow.Tests.Infrastructure;

internal static class TestSecretFactory
{
    public static string CreateValidPassword() =>
        $"Aa1!{Convert.ToHexString(RandomNumberGenerator.GetBytes(8))}";

    public static string CreateSetupCode() =>
        Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
}
