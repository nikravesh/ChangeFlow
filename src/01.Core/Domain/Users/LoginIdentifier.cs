namespace ChangeFlow.Core.Domain.Users;

public sealed record LoginIdentifier
{
    private LoginIdentifier(string original, string normalized)
    {
        Original = original;
        Normalized = normalized;
    }

    public string Original { get; }

    public string Normalized { get; }

    public static LoginIdentifier Create(string value)
    {
        ArgumentNullException.ThrowIfNull(value);

        var trimmed = value.Trim();
        if (trimmed.Length == 0)
        {
            throw new ArgumentException("Login identifier must not be empty.", nameof(value));
        }

        return new LoginIdentifier(trimmed, trimmed.ToUpperInvariant());
    }
}
