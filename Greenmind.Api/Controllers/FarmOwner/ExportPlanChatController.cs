using System.Security.Claims;
using System.Text.Json;
using Greenmind.Application.Contracts.Chat;
using Greenmind.Application.Dtos.Chat;
using Greenmind.Domain.Consts;
using Greenmind.Domain.Consts.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Microsoft.AspNetCore.RateLimiting;

namespace Greenmind.Api.Controllers.FarmOwner;

[ApiController]
[Route("api/export-plans/{exportPlanId:int}/chat/sessions")]
[Authorize(Roles = RoleNames.FarmOwner)]
[EnableRateLimiting("ai-chat")]
public sealed class ExportPlanChatController(
    IChatService chatService,
    IOptions<JsonOptions> jsonOptions) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<ChatSessionDto>>> ListSessions(
        int exportPlanId,
        CancellationToken cancellationToken) =>
        Ok(await chatService.ListPlanSessionsAsync(
            exportPlanId,
            GetOwnerUserId(),
            cancellationToken));

    [HttpPost]
    public async Task<ActionResult<ChatSessionDto>> CreateSession(
        int exportPlanId,
        [FromBody] CreateChatSessionRequest request,
        CancellationToken cancellationToken)
    {
        var result = await chatService.CreatePlanSessionAsync(
            exportPlanId,
            GetOwnerUserId(),
            request,
            cancellationToken);

        return result.Status == ChatOperationStatus.Succeeded
            ? StatusCode(StatusCodes.Status201Created, result.Value)
            : NotFound();
    }

    [HttpGet("{sessionId:int}/messages")]
    public async Task<ActionResult<ChatHistoryPageDto>> GetMessages(
        int exportPlanId,
        int sessionId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        CancellationToken cancellationToken = default)
    {
        var result = await chatService.GetMessagesAsync(
            ChatScope.ExportPlan,
            exportPlanId,
            sessionId,
            GetOwnerUserId(),
            page,
            pageSize,
            cancellationToken);

        return result.Status == ChatOperationStatus.Succeeded
            ? Ok(result.Value)
            : NotFound();
    }

    [HttpPost("{sessionId:int}/messages/stream")]
    [Produces("text/event-stream")]
    public async Task StreamMessage(
        int exportPlanId,
        int sessionId,
        [FromBody] SendChatMessageRequest request,
        CancellationToken cancellationToken)
    {
        var result = await chatService.StartReplyAsync(
            ChatScope.ExportPlan,
            exportPlanId,
            sessionId,
            GetOwnerUserId(),
            request,
            cancellationToken);

        if (result.Status != ChatOperationStatus.Succeeded)
        {
            Response.StatusCode = StatusCodes.Status404NotFound;
            return;
        }

        await WriteEventsAsync(result.Value!.Events, cancellationToken);
    }

    private async Task WriteEventsAsync(
        IAsyncEnumerable<ChatStreamEventDto> events,
        CancellationToken cancellationToken)
    {
        Response.StatusCode = StatusCodes.Status200OK;
        Response.ContentType = "text/event-stream; charset=utf-8";
        Response.Headers.CacheControl = "no-cache";
        Response.Headers["X-Accel-Buffering"] = "no";

        await foreach (var item in events.WithCancellation(cancellationToken))
        {
            var payload = JsonSerializer.Serialize(
                item,
                jsonOptions.Value.JsonSerializerOptions);
            await Response.WriteAsync(
                $"event: {item.Event}\ndata: {payload}\n\n",
                cancellationToken);
            await Response.Body.FlushAsync(cancellationToken);
        }
    }

    [HttpGet("{sessionId:int}/attachments/{attachmentId:int}")]
    public async Task<IActionResult> DownloadAttachment(
        int exportPlanId,
        int sessionId,
        int attachmentId,
        CancellationToken cancellationToken)
    {
        var result = await chatService.DownloadAttachmentAsync(
            ChatScope.ExportPlan,
            exportPlanId,
            sessionId,
            attachmentId,
            GetOwnerUserId(),
            cancellationToken);

        if (result.Status == ChatOperationStatus.NotFound)
            return NotFound();

        Response.Headers.CacheControl = "private, max-age=86400";
        Response.Headers["X-Content-Type-Options"] = "nosniff";
        return File(result.Value!.Content, result.Value.ContentType, result.Value.FileName, true);
    }

    private string GetOwnerUserId() =>
        User.FindFirstValue(ClaimTypes.NameIdentifier)
        ?? throw new InvalidOperationException("An authenticated user id is required.");
}
