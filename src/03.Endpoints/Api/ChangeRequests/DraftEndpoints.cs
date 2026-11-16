using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using ChangeFlow.Core.Application.ChangeRequests;
using ChangeFlow.Core.Domain.ChangeRequests;

namespace ChangeFlow.Endpoints.Api.ChangeRequests;

internal static class DraftEndpoints
{
    public static void MapDraftEndpoints(this RouteGroupBuilder group)
    {
        group.MapPost("", CreateAsync);
        group.MapGet("/{id}", ReadAsync);
        group.MapPut("/{id}", EditAsync);
        group.MapPost("/{id}/submit", SubmitAsync);
    }

    private static async Task<IResult> CreateAsync(HttpContext context, DraftWorkflow workflow)
    {
        var body = await ReadBodyAsync<CreateDraftBody>(context);
        if (body is null || ParseContent(body.Title, body.Description, body.Reason) is not { } content)
        {
            return InvalidInput(context);
        }

        var result = await workflow.CreateAsync(
            ChangeRequestRouteGroup.GetUserId(context.User), content, context.RequestAborted);
        return result.Failure is { } failure
            ? ChangeRequestRouteGroup.ToProblem(context, failure)
            : Results.Created($"/change-requests/{result.Value!.Id:D}", ToResponse(result.Value));
    }

    private static async Task<IResult> ReadAsync(string id, HttpContext context, ReviewWorkflow workflow)
    {
        if (!Guid.TryParseExact(id, "D", out var requestId))
        {
            return InvalidInput(context);
        }

        return ToResult(context, await workflow.ReadAsync(
            ChangeRequestRouteGroup.GetUserId(context.User), requestId, context.RequestAborted));
    }

    private static async Task<IResult> EditAsync(string id, HttpContext context, DraftWorkflow workflow)
    {
        if (!Guid.TryParseExact(id, "D", out var requestId))
        {
            return InvalidInput(context);
        }

        var body = await ReadBodyAsync<EditDraftBody>(context);
        if (body is null || !TryParseVersion(body.ExpectedVersion, out var version)
            || ParseContent(body.Title, body.Description, body.Reason) is not { } content)
        {
            return InvalidInput(context);
        }

        return ToResult(context, await workflow.EditAsync(
            ChangeRequestRouteGroup.GetUserId(context.User), requestId, version, content,
            context.RequestAborted));
    }

    private static async Task<IResult> SubmitAsync(string id, HttpContext context, DraftWorkflow workflow)
    {
        if (!Guid.TryParseExact(id, "D", out var requestId))
        {
            return InvalidInput(context);
        }

        var body = await ReadBodyAsync<SubmitDraftBody>(context);
        if (!TryParseVersion(body?.ExpectedVersion, out var version))
        {
            return InvalidInput(context);
        }

        return ToResult(context, await workflow.SubmitAsync(
            ChangeRequestRouteGroup.GetUserId(context.User), requestId, version, context.RequestAborted));
    }

    internal static async Task<T?> ReadBodyAsync<T>(HttpContext context) where T : class
    {
        if (!context.Request.HasJsonContentType())
        {
            return null;
        }

        try
        {
            return await context.Request.ReadFromJsonAsync<T>(context.RequestAborted);
        }
        catch (Exception exception) when (exception is JsonException or InvalidOperationException)
        {
            return null;
        }
    }

    private static DraftContent? ParseContent(string? title, string? description, string? reason) =>
        string.IsNullOrWhiteSpace(title) || string.IsNullOrWhiteSpace(description)
            || string.IsNullOrWhiteSpace(reason)
            ? null
            : new DraftContent(title, description, reason);

    internal static bool TryParseVersion(string? value, out long version)
    {
        version = 0;
        return value is { Length: > 0 and <= 19 } && value[0] is >= '1' and <= '9'
            && value.All(char.IsAsciiDigit)
            && long.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out version);
    }

    internal static IResult InvalidInput(HttpContext context) =>
        ChangeRequestRouteGroup.ToProblem(context, ChangeRequestFailure.ValidationFailed);

    internal static IResult ToResult(HttpContext context, ChangeRequestResult<ChangeRequestDetails> result) =>
        result.Failure is { } failure
            ? ChangeRequestRouteGroup.ToProblem(context, failure)
            : Results.Ok(ToResponse(result.Value!.Request, result.Value.CurrentFeedback));

    private static DraftResponse ToResponse(ChangeRequest request, string? feedback = null) =>
        new(request.Id, request.Title, request.Description, request.Reason, request.RequesterId.Value,
            request.Status.ToString(), request.Version.ToString(CultureInfo.InvariantCulture), feedback);

    private sealed record CreateDraftBody(string? Title, string? Description, string? Reason);
    private sealed record EditDraftBody(
        string? Title, string? Description, string? Reason, string? ExpectedVersion);
    private sealed record SubmitDraftBody(string? ExpectedVersion);
    private sealed record DraftResponse(
        Guid Id, string Title, string Description, string Reason, Guid RequesterId, string Status, string Version,
        [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? CurrentFeedback);
}
