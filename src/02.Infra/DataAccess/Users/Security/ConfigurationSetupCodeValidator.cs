using System.Security.Cryptography;
using System.Text;
using ChangeFlow.Core.Application.Users.CreateUser;
using Microsoft.Extensions.Configuration;

namespace ChangeFlow.Infra.DataAccess.Users.Security;

public sealed class ConfigurationSetupCodeValidator(IConfiguration configuration)
    : ISetupCodeValidator
{
    public const string ConfigurationKey = "Bootstrap:SetupCodeDigest";

    public bool IsValid(string? candidate)
    {
        if (string.IsNullOrEmpty(candidate))
        {
            return false;
        }

        var configuredDigest = ReadConfiguredDigest();
        if (configuredDigest is null)
        {
            return false;
        }

        var candidateDigest = SHA256.HashData(Encoding.UTF8.GetBytes(candidate));
        return CryptographicOperations.FixedTimeEquals(candidateDigest, configuredDigest);
    }

    private byte[]? ReadConfiguredDigest()
    {
        var encodedDigest = configuration[ConfigurationKey];
        if (string.IsNullOrWhiteSpace(encodedDigest))
        {
            return null;
        }

        try
        {
            var digest = Convert.FromHexString(encodedDigest);
            return digest.Length == SHA256.HashSizeInBytes ? digest : null;
        }
        catch (FormatException)
        {
            return null;
        }
    }
}
