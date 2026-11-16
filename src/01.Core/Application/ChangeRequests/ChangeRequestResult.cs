using ChangeFlow.Core.Domain.Users;

namespace ChangeFlow.Core.Application.ChangeRequests;

public enum ChangeRequestFailure
{
    AuthenticationRequired,
    ValidationFailed,
    Forbidden,
    SelfReviewForbidden,
    NotFound,
    VersionConflict,
    InvalidState,
    TemporarilyUnavailable,
    UnexpectedFailure
}

public sealed record ChangeRequestResult<T>
{
    private ChangeRequestResult(T? value, ChangeRequestFailure? failure)
    {
        Value = value;
        Failure = failure;
    }

    public T? Value { get; }
    public ChangeRequestFailure? Failure { get; }

    public static ChangeRequestResult<T> Success(T value) => new(value, null);
    public static ChangeRequestResult<T> Rejected(ChangeRequestFailure failure) => new(default, failure);
}

public static class ChangeRequestAccess
{
    public static ChangeRequestFailure? RequireRole(
        IReadOnlySet<UserRole>? currentRoles, UserRole requiredRole)
    {
        if (currentRoles is null)
        {
            return ChangeRequestFailure.AuthenticationRequired;
        }

        return currentRoles.Contains(requiredRole) ? null : ChangeRequestFailure.Forbidden;
    }

    public static ChangeRequestFailure ResolveContention(
        ChangeRequestFailure? accessFailure, long expectedVersion, long currentVersion)
    {
        if (accessFailure is { } failure)
        {
            return failure;
        }

        return expectedVersion == currentVersion
            ? ChangeRequestFailure.TemporarilyUnavailable
            : ChangeRequestFailure.VersionConflict;
    }
}
