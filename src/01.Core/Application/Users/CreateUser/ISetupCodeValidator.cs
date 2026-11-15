namespace ChangeFlow.Core.Application.Users.CreateUser;

public interface ISetupCodeValidator
{
    bool IsValid(string? candidate);
}
