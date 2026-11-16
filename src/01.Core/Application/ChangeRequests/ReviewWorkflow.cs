using ChangeFlow.Core.Domain.ChangeRequests;
using ChangeFlow.Core.Domain.Users;

namespace ChangeFlow.Core.Application.ChangeRequests;

public sealed record ChangeRequestDetails(ChangeRequest Request, string? CurrentFeedback);

public sealed class ReviewWorkflow(IChangeRequestTransaction transaction, TimeProvider clock)
{
    public Task<ChangeRequestResult<ChangeRequestDetails>> ReadAsync(
        UserId actor, Guid id, CancellationToken cancellationToken = default) =>
        transaction.ReadAsync(async (session, token) =>
        {
            var access = await LoadAsync(session, actor, id, token);
            if (access.Failure is { } failure)
            {
                return ChangeRequestResult<ChangeRequestDetails>.Rejected(failure);
            }

            var (request, roles) = access.Value;
            var isOwner = request.RequesterId == actor;
            if (!isOwner && (request.Status != ChangeRequestStatus.PendingReview
                || !roles.Contains(UserRole.Reviewer)))
            {
                return ChangeRequestResult<ChangeRequestDetails>.Rejected(ChangeRequestFailure.Forbidden);
            }

            var feedback = isOwner && request.Status == ChangeRequestStatus.ChangesRequested
                ? await session.ReadLatestFeedbackAsync(id, token)
                : null;
            return ChangeRequestResult<ChangeRequestDetails>.Success(new(request, feedback));
        }, cancellationToken);

    public Task<ChangeRequestResult<IReadOnlyList<ReviewDecision>>> HistoryAsync(
        UserId actor, Guid id, CancellationToken cancellationToken = default) =>
        transaction.ReadAsync(async (session, token) =>
        {
            var access = await LoadAsync(session, actor, id, token);
            if (access.Failure is { } failure)
            {
                return ChangeRequestResult<IReadOnlyList<ReviewDecision>>.Rejected(failure);
            }

            var (request, roles) = access.Value;
            if (request.RequesterId != actor && (!roles.Contains(UserRole.Reviewer)
                || !await session.HasDecisionAsync(id, actor, token)))
            {
                return ChangeRequestResult<IReadOnlyList<ReviewDecision>>.Rejected(ChangeRequestFailure.Forbidden);
            }

            return ChangeRequestResult<IReadOnlyList<ReviewDecision>>.Success(
                await session.ReadHistoryAsync(id, token));
        }, cancellationToken);

    public Task<ChangeRequestResult<ChangeRequestDetails>> ApproveAsync(
        UserId actor, Guid id, long expectedVersion, CancellationToken cancellationToken = default) =>
        DecideAsync(actor, id, expectedVersion, (request, now) => request.Approve(actor, now),
            cancellationToken);

    public Task<ChangeRequestResult<ChangeRequestDetails>> RequestChangesAsync(
        UserId actor, Guid id, long expectedVersion, string feedback,
        CancellationToken cancellationToken = default) =>
        DecideAsync(actor, id, expectedVersion, (request, now) => request.RequestChanges(actor, feedback, now),
            cancellationToken);

    private Task<ChangeRequestResult<ChangeRequestDetails>> DecideAsync(
        UserId actor, Guid id, long expectedVersion, Func<ChangeRequest, DateTimeOffset, ReviewDecision?> decide,
        CancellationToken cancellationToken) =>
        transaction.WriteAsync(async (session, token) =>
        {
            var access = await AuthorizeDecisionAsync(session, actor, id, token);
            if (access.Failure is { } failure)
            {
                return ChangeRequestResult<ChangeRequestDetails>.Rejected(failure);
            }

            var request = access.Value!;
            if (request.Version != expectedVersion)
            {
                return ChangeRequestResult<ChangeRequestDetails>.Rejected(ChangeRequestFailure.VersionConflict);
            }

            var decision = decide(request, clock.GetUtcNow());
            if (decision is null)
            {
                return ChangeRequestResult<ChangeRequestDetails>.Rejected(ChangeRequestFailure.InvalidState);
            }

            session.AddDecision(decision);
            return ChangeRequestResult<ChangeRequestDetails>.Success(new(request, decision.Feedback));
        }, async (session, token) =>
        {
            var access = await AuthorizeDecisionAsync(session, actor, id, token);
            return access.Failure ?? ChangeRequestAccess.ResolveContention(
                null, expectedVersion, access.Value!.Version);
        }, cancellationToken);

    private static async Task<ChangeRequestResult<ChangeRequest>> AuthorizeDecisionAsync(
        IChangeRequestSession session, UserId actor, Guid id, CancellationToken cancellationToken)
    {
        var access = await LoadAsync(session, actor, id, cancellationToken);
        if (access.Failure is { } failure)
        {
            return ChangeRequestResult<ChangeRequest>.Rejected(failure);
        }

        var (request, roles) = access.Value;
        if (!roles.Contains(UserRole.Reviewer))
        {
            return ChangeRequestResult<ChangeRequest>.Rejected(ChangeRequestFailure.Forbidden);
        }

        return request.RequesterId == actor
            ? ChangeRequestResult<ChangeRequest>.Rejected(ChangeRequestFailure.SelfReviewForbidden)
            : ChangeRequestResult<ChangeRequest>.Success(request);
    }

    private static async Task<ChangeRequestResult<(ChangeRequest Request, IReadOnlySet<UserRole> Roles)>> LoadAsync(
        IChangeRequestSession session, UserId actor, Guid id, CancellationToken cancellationToken)
    {
        var roles = await session.ReadRolesAsync(actor, cancellationToken);
        if (roles is null)
        {
            return ChangeRequestResult<(ChangeRequest, IReadOnlySet<UserRole>)>.Rejected(
                ChangeRequestFailure.AuthenticationRequired);
        }

        var request = await session.FindAsync(id, cancellationToken);
        return request is null
            ? ChangeRequestResult<(ChangeRequest, IReadOnlySet<UserRole>)>.Rejected(ChangeRequestFailure.NotFound)
            : ChangeRequestResult<(ChangeRequest, IReadOnlySet<UserRole>)>.Success((request, roles));
    }
}
