using System.Net.Http.Headers;
using ChangeFlow.Core.Application.Users.CreateUser;
using ChangeFlow.Core.Domain.Users;
using ChangeFlow.Endpoints.Api.Common.Errors;
using ChangeFlow.Endpoints.Api.Common.Observability;
using Microsoft.AspNetCore.Mvc;

namespace ChangeFlow.Endpoints.Api.Users.CreateUser;

public static class CreateUserEndpoint
{
    public static IEndpointRouteBuilder MapCreateUser(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost("/users", HandleAsync)
            .WithName("createUser")
            .Accepts<CreateUserRequest>("application/json")
            .Produces<CreateUserResponse>(StatusCodes.Status201Created)
            .Produces<ValidationProblemDetails>(
                StatusCodes.Status400BadRequest,
                "application/problem+json")
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status500InternalServerError);

        return endpoints;
    }

    private static async Task<IResult> HandleAsync(
        CreateUserRequest request,
        HttpContext httpContext,
        CreateUserUseCase useCase,
        CancellationToken cancellationToken)
    {
        var setupCode = ReadSetupCode(httpContext.Request.Headers.Authorization);
        var roles = request.Roles?.Select(ParseRole).ToArray();
        var result = await useCase.ExecuteAsync(
            new CreateUserCommand(
                setupCode,
                request.DisplayName,
                request.LoginIdentifier,
                request.Password,
                roles),
            cancellationToken);

        return result switch
        {
            CreateUserSuccess success => Created(httpContext, success),
            ValidationFailure failure => ValidationProblem(httpContext, failure),
            InvalidSetupCode => Unauthorized(httpContext),
            LoginIdentifierConflict => Conflict(httpContext),
            UnexpectedFailure => Unexpected(httpContext),
            _ => Unexpected(httpContext)
        };
    }

    private static UserRole ParseRole(string role) =>
        Enum.TryParse<UserRole>(role, ignoreCase: false, out var parsed)
        && Enum.IsDefined(parsed)
            ? parsed
            : (UserRole)(-1);

    private static IResult Created(HttpContext context, CreateUserSuccess success)
    {
        CorrelationMiddleware.SetOutcome(context, "created", LogLevel.Information);
        return Results.Json(
            new CreateUserResponse(
                success.Id.Value,
                success.DisplayName,
                success.LoginIdentifier,
                success.Roles.Select(role => role.ToString()).ToArray()),
            statusCode: StatusCodes.Status201Created);
    }

    private static IResult ValidationProblem(
        HttpContext context,
        ValidationFailure failure)
    {
        CorrelationMiddleware.SetOutcome(context, "validation_failed", LogLevel.Information);
        var problem = ProblemDetailsFactory.CreateValidation(
            context,
            failure.Errors,
            "validation_failed",
            "One or more request fields are invalid.");
        return Problem(problem);
    }

    private static IResult Unauthorized(HttpContext context)
    {
        CorrelationMiddleware.SetOutcome(context, "invalid_setup_code", LogLevel.Information);
        context.Response.Headers.WWWAuthenticate = "Setup";
        return Problem(ProblemDetailsFactory.Create(
            context,
            StatusCodes.Status401Unauthorized,
            "invalid_setup_code",
            "A valid setup credential is required."));
    }

    private static IResult Conflict(HttpContext context)
    {
        CorrelationMiddleware.SetOutcome(context, "login_identifier_conflict", LogLevel.Warning);
        return Problem(ProblemDetailsFactory.Create(
            context,
            StatusCodes.Status409Conflict,
            "login_identifier_conflict",
            "The login identifier is already in use."));
    }

    private static IResult Unexpected(HttpContext context)
    {
        CorrelationMiddleware.SetOutcome(context, "unexpected_failure", LogLevel.Error);
        return Problem(ProblemDetailsFactory.Create(
            context,
            StatusCodes.Status500InternalServerError,
            "unexpected_failure",
            "An unexpected error occurred."));
    }

    private static IResult Problem(ProblemDetails problem) => Results.Json(
        problem,
        statusCode: problem.Status,
        contentType: "application/problem+json");

    private static string? ReadSetupCode(string? authorization)
    {
        if (!AuthenticationHeaderValue.TryParse(authorization, out var header)
            || !string.Equals(header.Scheme, "Setup", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        return header.Parameter;
    }
}
