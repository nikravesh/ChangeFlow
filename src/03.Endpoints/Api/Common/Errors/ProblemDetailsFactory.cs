using Microsoft.AspNetCore.Mvc;

namespace ChangeFlow.Endpoints.Api.Common.Errors;

public static class ProblemDetailsFactory
{
    public static ProblemDetails Create(
        HttpContext httpContext,
        int status,
        string code,
        string title,
        string? type = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(code);

        var problem = new ProblemDetails
        {
            Status = status,
            Title = title,
            Type = type ?? $"/problems/{code}",
            Instance = httpContext.Request.Path
        };

        problem.Extensions["code"] = code;
        problem.Extensions["traceId"] = httpContext.TraceIdentifier;

        return problem;
    }

    public static ValidationProblemDetails CreateValidation(
        HttpContext httpContext,
        IReadOnlyDictionary<string, string[]> errors,
        string code,
        string title)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(code);

        var problem = new ValidationProblemDetails(
            errors.ToDictionary(pair => pair.Key, pair => pair.Value))
        {
            Status = StatusCodes.Status400BadRequest,
            Title = title,
            Type = $"/problems/{code}",
            Instance = httpContext.Request.Path
        };

        problem.Extensions["code"] = code;
        problem.Extensions["traceId"] = httpContext.TraceIdentifier;

        return problem;
    }
}
