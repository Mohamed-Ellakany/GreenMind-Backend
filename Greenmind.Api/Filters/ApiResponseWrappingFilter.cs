using Greenmind.Api.Logging;
using Greenmind.Application.Contracts.Api;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace Greenmind.Api.Filters;

public sealed class ApiResponseWrappingFilter(ILoggerFactory loggerFactory) : IAsyncResultFilter
{
    private readonly ILogger _authLogger = loggerFactory.CreateLogger(LoggingCategories.Authentication);
    private readonly ILogger _authorizationLogger = loggerFactory.CreateLogger(LoggingCategories.Authorization);

    public async Task OnResultExecutionAsync(ResultExecutingContext context, ResultExecutionDelegate next)
    {
        context.Result = context.Result switch
        {
            ObjectResult objectResult => WrapObjectResult(context, objectResult),
            JsonResult jsonResult => WrapJsonResult(context, jsonResult),
            StatusCodeResult statusCodeResult => WrapStatusCodeResult(context, statusCodeResult),
            _ => context.Result
        };

        await next();
    }

    private IActionResult WrapObjectResult(ResultExecutingContext context, ObjectResult result)
    {
        if (result.Value is IApiResponse)
        {
            return result;
        }

        var statusCode = result.StatusCode ?? StatusCodes.Status200OK;
        var traceId = context.HttpContext.TraceIdentifier;

        if (IsSuccessStatusCode(statusCode))
        {
            result.Value = ApiResponseFactory.Success(result.Value, traceId: traceId);
            return result;
        }

        LogAuthorizationFailureIfNeeded(context, statusCode, traceId);
        result.Value = ApiResponseFactory.FromStatusCode<object?>(
            statusCode,
            traceId,
            ExtractErrorMessage(result.Value));

        return result;
    }

    private IActionResult WrapJsonResult(ResultExecutingContext context, JsonResult result)
    {
        if (result.Value is IApiResponse)
        {
            return result;
        }

        var statusCode = result.StatusCode ?? StatusCodes.Status200OK;
        var traceId = context.HttpContext.TraceIdentifier;

        result.Value = IsSuccessStatusCode(statusCode)
            ? ApiResponseFactory.Success<object?>(result.Value, traceId: traceId)
            : ApiResponseFactory.FromStatusCode<object?>(statusCode, traceId, ExtractErrorMessage(result.Value));

        LogAuthorizationFailureIfNeeded(context, statusCode, traceId);
        return result;
    }

    private IActionResult WrapStatusCodeResult(ResultExecutingContext context, StatusCodeResult result)
    {
        var traceId = context.HttpContext.TraceIdentifier;

        if (IsSuccessStatusCode(result.StatusCode))
        {
            return new ObjectResult(ApiResponseFactory.Success<object?>(null, traceId: traceId))
            {
                StatusCode = result.StatusCode
            };
        }

        LogAuthorizationFailureIfNeeded(context, result.StatusCode, traceId);

        return new ObjectResult(ApiResponseFactory.FromStatusCode<object?>(result.StatusCode, traceId))
        {
            StatusCode = result.StatusCode
        };
    }

    private void LogAuthorizationFailureIfNeeded(ResultExecutingContext context, int statusCode, string traceId)
    {
        if (statusCode != StatusCodes.Status401Unauthorized && statusCode != StatusCodes.Status403Forbidden)
        {
            return;
        }

        var logger = statusCode == StatusCodes.Status401Unauthorized ? _authLogger : _authorizationLogger;

        logger.LogWarning(
            "Request ended with {StatusCode} for {Method} {Path}. TraceId: {TraceId}",
            statusCode,
            context.HttpContext.Request.Method,
            context.HttpContext.Request.Path.Value,
            traceId);
    }

    private static bool IsSuccessStatusCode(int statusCode)
    {
        return statusCode is >= StatusCodes.Status200OK and < StatusCodes.Status300MultipleChoices;
    }

    private static string? ExtractErrorMessage(object? value)
    {
        return value switch
        {
            null => null,
            string message => message,
            ProblemDetails problemDetails => problemDetails.Detail ?? problemDetails.Title,
            _ => null
        };
    }
}
