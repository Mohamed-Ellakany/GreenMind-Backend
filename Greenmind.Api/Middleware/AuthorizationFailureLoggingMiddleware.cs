using Greenmind.Api.Logging;
using Greenmind.Application.Contracts.Api;

namespace Greenmind.Api.Middleware;

public sealed class AuthorizationFailureLoggingMiddleware(RequestDelegate next, ILoggerFactory loggerFactory)
{
    private readonly ILogger _authLogger = loggerFactory.CreateLogger(LoggingCategories.Authentication);
    private readonly ILogger _authorizationLogger = loggerFactory.CreateLogger(LoggingCategories.Authorization);

    public async Task InvokeAsync(HttpContext context)
    {
        await next(context);

        if (context.Response.StatusCode is not StatusCodes.Status401Unauthorized
            and not StatusCodes.Status403Forbidden)
        {
            return;
        }

        var statusCode = context.Response.StatusCode;
        var traceId = context.TraceIdentifier;
        var logger = statusCode == StatusCodes.Status401Unauthorized ? _authLogger : _authorizationLogger;

        logger.LogWarning(
            "Request ended with {StatusCode} for {Method} {Path}. TraceId: {TraceId}",
            statusCode,
            context.Request.Method,
            context.Request.Path.Value,
            traceId);

        if (context.Response.HasStarted || context.Response.ContentLength is not null)
        {
            return;
        }

        context.Response.ContentType = "application/json";

        await context.Response.WriteAsJsonAsync(
            ApiResponseFactory.FromStatusCode<object?>(statusCode, traceId));
    }
}
