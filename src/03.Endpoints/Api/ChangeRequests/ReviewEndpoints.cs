using ChangeFlow.Core.Application.ChangeRequests;
using static ChangeFlow.Endpoints.Api.ChangeRequests.DraftEndpoints;

namespace ChangeFlow.Endpoints.Api.ChangeRequests;

internal static class ReviewEndpoints
{
    public static void MapReviewEndpoints(this RouteGroupBuilder group)
    {
        group.MapPost("/{id}/approve", ApproveAsync);
        group.MapPost("/{id}/request-changes", RequestChangesAsync);
        group.MapGet("/{id}/review-history", HistoryAsync);
    }

    private static async Task<IResult> ApproveAsync(string id, HttpContext context, ReviewWorkflow workflow)
    {
        if (!Guid.TryParseExact(id, "D", out var requestId))
        {
            return InvalidInput(context);
        }

        var body = await ReadBodyAsync<ApproveBody>(context);
        if (!TryParseVersion(body?.ExpectedVersion, out var version))
        {
            return InvalidInput(context);
        }

        return ToResult(context, await workflow.ApproveAsync(
            ChangeRequestRouteGroup.GetUserId(context.User), requestId, version, context.RequestAborted));
    }

    private static async Task<IResult> RequestChangesAsync(string id, HttpContext context, ReviewWorkflow workflow)
    {
        if (!Guid.TryParseExact(id, "D", out var requestId))
        {
            return InvalidInput(context);
        }

        var body = await ReadBodyAsync<RequestChangesBody>(context);
        if (body is null || !TryParseVersion(body.ExpectedVersion, out var version)
            || string.IsNullOrWhiteSpace(body.Feedback))
        {
            return InvalidInput(context);
        }

        return ToResult(context, await workflow.RequestChangesAsync(
            ChangeRequestRouteGroup.GetUserId(context.User), requestId, version, body.Feedback,
            context.RequestAborted));
    }

    private static async Task<IResult> HistoryAsync(string id, HttpContext context, ReviewWorkflow workflow)
    {
        if (!Guid.TryParseExact(id, "D", out var requestId))
        {
            return InvalidInput(context);
        }

        var result = await workflow.HistoryAsync(
            ChangeRequestRouteGroup.GetUserId(context.User), requestId, context.RequestAborted);
        return result.Failure is { } failure
            ? ChangeRequestRouteGroup.ToProblem(context, failure)
            : Results.Ok(result.Value!.Select(decision => new ReviewDecisionResponse(
                decision.ReviewerId.Value, decision.DecidedAtUtc, decision.Action.ToString(), decision.Feedback)));
    }

    private sealed record ApproveBody(string? ExpectedVersion);
    private sealed record RequestChangesBody(string? ExpectedVersion, string? Feedback);
    private sealed record ReviewDecisionResponse(Guid ReviewerId, DateTimeOffset DecidedAtUtc,
        string Action, string? Feedback);
}
