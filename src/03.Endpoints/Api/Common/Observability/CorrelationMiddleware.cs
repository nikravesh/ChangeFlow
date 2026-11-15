using System.Diagnostics;

namespace ChangeFlow.Endpoints.Api.Common.Observability;

public sealed class CorrelationMiddleware(
    RequestDelegate next,
    ILogger<CorrelationMiddleware> logger)
{
    public const string HeaderName = "X-Correlation-ID";
    public const string RedactedValue = "[REDACTED]";
    private const string OutcomeItemKey = "ChangeFlow.RequestOutcome";

    private static readonly HashSet<string> SensitiveFieldNames =
        new(StringComparer.OrdinalIgnoreCase)
        {
            "Authorization",
            "password",
            "passwordHash",
            "setupCode"
        };

    public async Task InvokeAsync(HttpContext context)
    {
        var correlationId = GetCorrelationId(context);
        context.TraceIdentifier = correlationId;
        context.Response.Headers[HeaderName] = correlationId;

        using var scope = logger.BeginScope(new Dictionary<string, object>
        {
            ["CorrelationId"] = correlationId
        });

        var stopwatch = Stopwatch.StartNew();

        try
        {
            await next(context);
        }
        finally
        {
            stopwatch.Stop();
            if (context.Items.TryGetValue(OutcomeItemKey, out var value)
                && value is RequestOutcome outcome)
            {
                logger.Log(
                    outcome.Level,
                    "HTTP {Method} {Path} completed with {StatusCode}, outcome {OutcomeCode}, in {ElapsedMilliseconds} ms",
                    context.Request.Method,
                    context.Request.Path.Value,
                    context.Response.StatusCode,
                    outcome.Code,
                    stopwatch.Elapsed.TotalMilliseconds);
            }
            else
            {
                logger.LogInformation(
                    "HTTP {Method} {Path} completed with {StatusCode} in {ElapsedMilliseconds} ms",
                    context.Request.Method,
                    context.Request.Path.Value,
                    context.Response.StatusCode,
                    stopwatch.Elapsed.TotalMilliseconds);
            }
        }
    }

    public static void SetOutcome(HttpContext context, string code, LogLevel level)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(code);
        context.Items[OutcomeItemKey] = new RequestOutcome(code, level);
    }

    public static object? Redact(string fieldName, object? value) =>
        SensitiveFieldNames.Contains(fieldName) ? RedactedValue : value;

    private static string GetCorrelationId(HttpContext context)
    {
        var supplied = context.Request.Headers[HeaderName].FirstOrDefault();

        if (!string.IsNullOrWhiteSpace(supplied)
            && supplied.Length <= 128
            && supplied.All(character => char.IsLetterOrDigit(character) || character is '-' or '_'))
        {
            return supplied;
        }

        return Activity.Current?.TraceId.ToString() ?? Guid.NewGuid().ToString("N");
    }

    private sealed record RequestOutcome(string Code, LogLevel Level);
}
