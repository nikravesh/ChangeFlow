using System.Security.Claims;
using ChangeFlow.Core.Application.ChangeRequests;
using ChangeFlow.Core.Domain.Users;
using ChangeFlow.Endpoints.Api.Common.Errors;
using ChangeFlow.Endpoints.Api.Common.Observability;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;

namespace ChangeFlow.Endpoints.Api.ChangeRequests;

public static class ChangeRequestRouteGroup
{
    public const string Scheme = "ChangeRequestsBearer";
    private const string Policy = "ChangeRequests";

    public static IServiceCollection AddChangeRequestAuthentication(
        this IServiceCollection services)
    {
        services.AddOptions<JwtBearerOptions>(Scheme).Configure<IConfiguration>((options, configuration) =>
        {
            var settings = configuration.GetSection(ChangeRequestAuthenticationOptions.SectionName)
                .Get<ChangeRequestAuthenticationOptions>() ?? new();
            options.Authority = string.IsNullOrWhiteSpace(settings.Authority) ? null : settings.Authority;
            options.Audience = string.IsNullOrWhiteSpace(settings.Audience) ? null : settings.Audience;
        });
        services.AddAuthentication().AddJwtBearer(Scheme, options =>
        {
            options.MapInboundClaims = false;
            options.IncludeErrorDetails = false;
            options.TokenValidationParameters.ValidateIssuer = true;
            options.TokenValidationParameters.ValidateAudience = true;
            options.TokenValidationParameters.ValidateLifetime = true;
            options.TokenValidationParameters.RequireExpirationTime = true;
            options.TokenValidationParameters.RequireSignedTokens = true;
            options.TokenValidationParameters.ValidateIssuerSigningKey = true;
            options.TokenValidationParameters.ClockSkew = TimeSpan.Zero;
            options.Events = new JwtBearerEvents
            {
                OnMessageReceived = context =>
                {
                    var policies = context.HttpContext.GetEndpoint()?.Metadata
                        .GetOrderedMetadata<IAuthorizeData>();
                    if (policies?.Any(metadata => metadata.Policy == Policy) != true)
                    {
                        context.NoResult();
                    }

                    return Task.CompletedTask;
                },
                OnTokenValidated = context =>
                {
                    if (ReadUserId(context.Principal) is null)
                    {
                        context.Fail("The token subject is invalid.");
                    }

                    return Task.CompletedTask;
                },
                OnChallenge = context =>
                {
                    context.HandleResponse();
                    return ToProblem(context.HttpContext, ChangeRequestFailure.AuthenticationRequired)
                        .ExecuteAsync(context.HttpContext);
                }
            };
        });
        services.AddAuthorizationBuilder().AddPolicy(Policy, policy =>
            policy.AddAuthenticationSchemes(Scheme).RequireAuthenticatedUser());
        return services;
    }

    public static RouteGroupBuilder MapChangeRequests(this IEndpointRouteBuilder endpoints) =>
        endpoints.MapGroup("/change-requests").RequireAuthorization(Policy)
            .AddEndpointFilter(HandleUnexpectedFailureAsync);

    internal static void MapChangeRequestEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapChangeRequests();
        group.MapDraftEndpoints();
        group.MapReviewEndpoints();
    }

    public static UserId GetUserId(ClaimsPrincipal principal) =>
        ReadUserId(principal) ?? throw new InvalidOperationException("An authenticated subject is required.");

    public static IResult ToProblem(HttpContext context, ChangeRequestFailure failure)
    {
        var (status, code, title) = failure switch
        {
            ChangeRequestFailure.AuthenticationRequired =>
                (401, "authentication_required", "A valid bearer identity is required."),
            ChangeRequestFailure.ValidationFailed =>
                (400, "validation_failed", "One or more request fields are invalid."),
            ChangeRequestFailure.Forbidden =>
                (403, "forbidden", "Access to the operation is forbidden."),
            ChangeRequestFailure.SelfReviewForbidden =>
                (403, "self_review_forbidden", "Reviewing your own request is forbidden."),
            ChangeRequestFailure.NotFound =>
                (404, "change_request_not_found", "The change request was not found."),
            ChangeRequestFailure.VersionConflict =>
                (409, "version_conflict", "The change request version has changed."),
            ChangeRequestFailure.InvalidState =>
                (409, "invalid_state", "The change request state does not allow this operation."),
            ChangeRequestFailure.TemporarilyUnavailable =>
                (503, "temporarily_unavailable", "The change request is temporarily unavailable."),
            ChangeRequestFailure.UnexpectedFailure =>
                (500, "unexpected_failure", "An unexpected error occurred."),
            _ => throw new ArgumentOutOfRangeException(nameof(failure))
        };

        if (failure == ChangeRequestFailure.AuthenticationRequired)
        {
            context.Response.Headers.WWWAuthenticate = "Bearer";
        }

        CorrelationMiddleware.SetOutcome(context, code, LogLevel.Information);
        return Results.Json(ProblemDetailsFactory.Create(context, status, code, title),
            statusCode: status, contentType: "application/problem+json");
    }

    private static async ValueTask<object?> HandleUnexpectedFailureAsync(
        EndpointFilterInvocationContext invocation, EndpointFilterDelegate next)
    {
        var context = invocation.HttpContext;
        try
        {
            return await next(invocation);
        }
        catch (Exception exception) when (!context.RequestAborted.IsCancellationRequested)
        {
            var logger = context.RequestServices.GetRequiredService<ILoggerFactory>()
                .CreateLogger(typeof(ChangeRequestRouteGroup).FullName!);
            logger.LogError("Change request operation failed with {ExceptionType}; correlation {CorrelationId}",
                exception.GetType().FullName, context.TraceIdentifier);
            return ToProblem(context, ChangeRequestFailure.UnexpectedFailure);
        }
    }

    private static UserId? ReadUserId(ClaimsPrincipal? principal)
    {
        var subjects = principal?.FindAll("sub").Take(2).ToArray();
        return subjects is { Length: 1 }
            && Guid.TryParseExact(subjects[0].Value, "D", out var id) && id != Guid.Empty
            ? new UserId(id)
            : null;
    }
}
