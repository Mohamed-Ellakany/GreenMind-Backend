using Greenmind.Application.Contracts.Api;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace Greenmind.Api.Filters;

public sealed class ModelStateValidationFilter(ILogger<ModelStateValidationFilter> logger) : IActionFilter
{
    public void OnActionExecuting(ActionExecutingContext context)
    {
        if (context.ModelState.IsValid)
        {
            return;
        }

        var errors = context.ModelState
            .Where(entry => entry.Value?.Errors.Count > 0)
            .ToDictionary(
                entry => string.IsNullOrWhiteSpace(entry.Key) ? "request" : entry.Key,
                entry => entry.Value!.Errors
                    .Select(error => string.IsNullOrWhiteSpace(error.ErrorMessage)
                        ? "The input was not valid."
                        : error.ErrorMessage)
                    .ToArray());

        var traceId = context.HttpContext.TraceIdentifier;

        logger.LogWarning(
            "Validation failed for {Method} {Path}. TraceId: {TraceId}. Fields: {Fields}",
            context.HttpContext.Request.Method,
            context.HttpContext.Request.Path.Value,
            traceId,
            errors.Keys);

        context.Result = new BadRequestObjectResult(
            ApiResponseFactory.ValidationFailure<object?>(errors, traceId));
    }

    public void OnActionExecuted(ActionExecutedContext context)
    {
    }
}
