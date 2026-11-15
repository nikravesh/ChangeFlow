namespace ChangeFlow.Core.Application.Users.CreateUser;

public static class PasswordPolicy
{
    public static IReadOnlyList<string> Validate(string? password)
    {
        var failures = new List<string>();

        if (password is null || password.Length < 8)
        {
            failures.Add("Password must contain at least 8 characters.");
        }

        if (password is null || !password.Any(char.IsUpper))
        {
            failures.Add("Password must contain an uppercase letter.");
        }

        if (password is null || !password.Any(char.IsLower))
        {
            failures.Add("Password must contain a lowercase letter.");
        }

        if (password is null || !password.Any(char.IsDigit))
        {
            failures.Add("Password must contain a number.");
        }

        if (password is null || !password.Any(character => !char.IsLetterOrDigit(character)))
        {
            failures.Add("Password must contain a symbol.");
        }

        return failures;
    }
}
