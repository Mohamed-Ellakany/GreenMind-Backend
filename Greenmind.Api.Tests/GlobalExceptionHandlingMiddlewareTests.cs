using Greenmind.Api.Middleware;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Greenmind.Api.Tests;

public sealed class GlobalExceptionHandlingMiddlewareTests
{
    [Fact]
    public async Task InvokeAsync_ClientAbort_IsNotConvertedToInternalServerError()
    {
        using var requestCancellation = new CancellationTokenSource();
        requestCancellation.Cancel();
        var context = new DefaultHttpContext
        {
            RequestAborted = requestCancellation.Token
        };
        var middleware = new GlobalExceptionHandlingMiddleware(
            _ => throw new TaskCanceledException(),
            NullLoggerFactory.Instance);

        await middleware.InvokeAsync(context);

        Assert.Equal(StatusCodes.Status499ClientClosedRequest, context.Response.StatusCode);
    }

    [Fact]
    public async Task InvokeAsync_UnrelatedCancellation_RemainsInternalServerError()
    {
        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();
        var middleware = new GlobalExceptionHandlingMiddleware(
            _ => throw new TaskCanceledException(),
            NullLoggerFactory.Instance);

        await middleware.InvokeAsync(context);

        Assert.Equal(StatusCodes.Status500InternalServerError, context.Response.StatusCode);
    }
}
