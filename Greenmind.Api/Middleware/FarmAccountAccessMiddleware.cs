using System.Security.Claims;
using Greenmind.Api.Logging;
using Greenmind.Application.Contracts.Api;
using Greenmind.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Greenmind.Api.Middleware;

public sealed class FarmAccountAccessMiddleware(RequestDelegate next, ILoggerFactory loggerFactory)
{
    private readonly ILogger _logger = loggerFactory.CreateLogger(LoggingCategories.Authorization);

    public async Task InvokeAsync(HttpContext context, GreenmindDbContext dbContext)
    {
        if (context.User.Identity?.IsAuthenticated != true)
        {
            await next(context);
            return;
        }

        var userId = context.User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(userId))
        {
            await next(context);
            return;
        }

        var account = await dbContext.Users
            .AsNoTracking()
            .Where(user => user.Id == userId)
            .Select(user => new
            {
                user.IsActive
            })
            .SingleOrDefaultAsync(context.RequestAborted);

        if (account is null || !account.IsActive)
        {
            _logger.LogWarning(
                "Blocked inactive account {UserId} from {Method} {Path}. TraceId: {TraceId}",
                userId,
                context.Request.Method,
                context.Request.Path.Value,
                context.TraceIdentifier);

            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsJsonAsync(
                ApiResponseFactory.FromStatusCode<object?>(
                    StatusCodes.Status403Forbidden,
                    context.TraceIdentifier,
                    "The account is inactive or pending email activation."),
                context.RequestAborted);
            return;
        }

        await next(context);
    }
}
