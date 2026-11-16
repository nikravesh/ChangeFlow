using ChangeFlow.Core.Domain.Users;

namespace ChangeFlow.Core.Application.Users.CreateUser;

public sealed class CreateUserUseCase(
    ISetupCodeValidator setupCodeValidator,
    IPasswordCredentialHasher passwordHasher,
    IUserProvisioningStore store)
{
    public async Task<CreateUserResult> ExecuteAsync(
        CreateUserCommand command,
        CancellationToken cancellationToken = default)
    {
        if (!setupCodeValidator.IsValid(command.SetupCode))
        {
            return new InvalidSetupCode();
        }

        var errors = new Dictionary<string, string[]>(StringComparer.Ordinal);

        if (string.IsNullOrWhiteSpace(command.DisplayName))
        {
            errors["displayName"] = ["Display name must not be empty."];
        }

        if (string.IsNullOrWhiteSpace(command.LoginIdentifier))
        {
            errors["loginIdentifier"] = ["Login identifier must not be empty."];
        }

        var passwordFailures = PasswordPolicy.Validate(command.Password);
        if (passwordFailures.Count > 0)
        {
            errors["password"] = passwordFailures.ToArray();
        }

        var roles = command.Roles?.Distinct().ToArray() ?? [];
        if (roles.Length == 0 || roles.Any(role => !Enum.IsDefined(role)))
        {
            errors["roles"] = ["One or two approved roles are required."];
        }

        if (errors.Count > 0)
        {
            return new ValidationFailure(errors);
        }

        var stage = "create_user";
        try
        {
            var user = User.Create(
                command.DisplayName!,
                command.LoginIdentifier!,
                roles);
            stage = "hashing";
            var passwordHash = passwordHasher.Hash(user, command.Password!);

            stage = "persistence";
            var persistenceFailure = await store.ProvisionAsync(
                user,
                passwordHash,
                cancellationToken);
            if (persistenceFailure is not null)
            {
                return persistenceFailure;
            }

            return new CreateUserSuccess(
                user.Id,
                user.DisplayName,
                user.LoginIdentifier.Original,
                user.Roles.ToArray());
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            return new UnexpectedFailure(stage, exception.GetType().Name);
        }
    }
}
