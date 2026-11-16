using ChangeFlow.Core.Domain.ChangeRequests;
using ChangeFlow.Core.Domain.Users;

namespace ChangeFlow.Core.Application.ChangeRequests;

public sealed class DraftWorkflow(IChangeRequestTransaction transaction)
{
    public Task<ChangeRequestResult<ChangeRequest>> CreateAsync(
        UserId actor, DraftContent content, CancellationToken cancellationToken = default) =>
        transaction.WriteAsync(async (session, token) =>
        {
            var failure = await RequireRequesterAsync(session, actor, token);
            if (failure is { } error)
            {
                return ChangeRequestResult<ChangeRequest>.Rejected(error);
            }

            var request = ChangeRequest.Create(actor, content);
            session.Add(request);
            return ChangeRequestResult<ChangeRequest>.Success(request);
        }, async (session, token) =>
            await RequireRequesterAsync(session, actor, token)
            ?? ChangeRequestFailure.TemporarilyUnavailable, cancellationToken);

    public Task<ChangeRequestResult<ChangeRequestDetails>> EditAsync(
        UserId actor, Guid id, long expectedVersion, DraftContent content,
        CancellationToken cancellationToken = default) =>
        ChangeAsync(actor, id, expectedVersion, request => request.Edit(content), cancellationToken);

    public Task<ChangeRequestResult<ChangeRequestDetails>> SubmitAsync(
        UserId actor, Guid id, long expectedVersion, CancellationToken cancellationToken = default) =>
        ChangeAsync(actor, id, expectedVersion, request => request.Submit(), cancellationToken);

    private Task<ChangeRequestResult<ChangeRequestDetails>> ChangeAsync(
        UserId actor, Guid id, long expectedVersion, Func<ChangeRequest, bool> change,
        CancellationToken cancellationToken) =>
        transaction.WriteAsync(async (session, token) =>
        {
            var access = await ReadOwnedAsync(session, actor, id, token);
            if (access.Failure is { } failure)
            {
                return ChangeRequestResult<ChangeRequestDetails>.Rejected(failure);
            }

            var request = access.Value!;
            if (request.Version != expectedVersion)
            {
                return ChangeRequestResult<ChangeRequestDetails>.Rejected(ChangeRequestFailure.VersionConflict);
            }

            if (!change(request))
            {
                return ChangeRequestResult<ChangeRequestDetails>.Rejected(ChangeRequestFailure.InvalidState);
            }

            var feedback = request.Status == ChangeRequestStatus.ChangesRequested
                ? await session.ReadLatestFeedbackAsync(id, token)
                : null;
            return ChangeRequestResult<ChangeRequestDetails>.Success(new(request, feedback));
        }, async (session, token) =>
        {
            var access = await ReadOwnedAsync(session, actor, id, token);
            return access.Failure ?? ChangeRequestAccess.ResolveContention(
                null, expectedVersion, access.Value!.Version);
        }, cancellationToken);

    private static async Task<ChangeRequestResult<ChangeRequest>> ReadOwnedAsync(
        IChangeRequestSession session, UserId actor, Guid id,
        CancellationToken cancellationToken)
    {
        var roles = await session.ReadRolesAsync(actor, cancellationToken);
        if (roles is null)
        {
            return ChangeRequestResult<ChangeRequest>.Rejected(ChangeRequestFailure.AuthenticationRequired);
        }

        var request = await session.FindAsync(id, cancellationToken);
        if (request is null)
        {
            return ChangeRequestResult<ChangeRequest>.Rejected(ChangeRequestFailure.NotFound);
        }

        if (request.RequesterId != actor || !roles.Contains(UserRole.Requester))
        {
            return ChangeRequestResult<ChangeRequest>.Rejected(ChangeRequestFailure.Forbidden);
        }

        return ChangeRequestResult<ChangeRequest>.Success(request);
    }

    private static async Task<ChangeRequestFailure?> RequireRequesterAsync(
        IChangeRequestSession session, UserId actor, CancellationToken cancellationToken) =>
        ChangeRequestAccess.RequireRole(
            await session.ReadRolesAsync(actor, cancellationToken), UserRole.Requester);
}
