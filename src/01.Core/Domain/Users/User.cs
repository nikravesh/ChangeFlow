namespace ChangeFlow.Core.Domain.Users;

public sealed class User
{
    private readonly IReadOnlySet<UserRole> roles;

    private User(
        UserId id,
        string displayName,
        LoginIdentifier loginIdentifier,
        IReadOnlySet<UserRole> roles)
    {
        Id = id;
        DisplayName = displayName;
        LoginIdentifier = loginIdentifier;
        this.roles = roles;
    }

    public UserId Id { get; }

    public string DisplayName { get; }

    public LoginIdentifier LoginIdentifier { get; }

    public IReadOnlySet<UserRole> Roles => roles;

    public static User Create(
        string displayName,
        string loginIdentifier,
        IEnumerable<UserRole> roles)
    {
        ArgumentNullException.ThrowIfNull(displayName);
        ArgumentNullException.ThrowIfNull(roles);

        var trimmedName = displayName.Trim();
        if (trimmedName.Length == 0)
        {
            throw new ArgumentException("Display name must not be empty.", nameof(displayName));
        }

        var uniqueRoles = roles.ToHashSet();
        if (uniqueRoles.Count is < 1 or > 2
            || uniqueRoles.Any(role => !Enum.IsDefined(role)))
        {
            throw new ArgumentException(
                "One or two approved roles are required.",
                nameof(roles));
        }

        return new User(
            UserId.New(),
            trimmedName,
            LoginIdentifier.Create(loginIdentifier),
            uniqueRoles);
    }
}
