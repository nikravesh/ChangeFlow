namespace ChangeFlow.Core.Application.Users.CreateUser;

public abstract record CreateUserFailure : CreateUserResult;

public sealed record ValidationFailure(
    IReadOnlyDictionary<string, string[]> Errors) : CreateUserFailure;

public sealed record InvalidSetupCode : CreateUserFailure;

public sealed record LoginIdentifierConflict : CreateUserFailure;

public sealed record UnexpectedFailure : CreateUserFailure;
