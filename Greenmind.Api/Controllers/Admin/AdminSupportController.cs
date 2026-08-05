using System.Security.Claims;
using Greenmind.Api.Hubs;
using Greenmind.Application.Contracts.Support;
using Greenmind.Application.Dtos.Support;
using Greenmind.Domain.Consts;
using Greenmind.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace Greenmind.Api.Controllers.Admin;

[ApiController]
[Route("api/admin/support/conversations")]
[Authorize(Roles = RoleNames.Admin)]
public sealed class AdminSupportController(
    ISupportService supportService,
    GreenmindDbContext dbContext,
    IHubContext<SupportHub> hubContext) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<SupportConversationDto>>> List(
        CancellationToken cancellationToken) =>
        Ok(await supportService.ListAdminConversationsAsync(cancellationToken));

    [HttpGet("{conversationId:int}/messages")]
    public async Task<ActionResult<SupportMessagePageDto>> GetMessages(
        int conversationId,
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 100,
        CancellationToken cancellationToken = default) =>
        Map(await supportService.GetAdminMessagesAsync(
            conversationId,
            pageNumber,
            pageSize,
            cancellationToken));

    [HttpPost("{conversationId:int}/messages")]
    public async Task<ActionResult<SupportMessageDto>> SendMessage(
        int conversationId,
        [FromBody] SendSupportMessageRequest request,
        CancellationToken cancellationToken)
    {
        var ownerUserId = await dbContext.SupportConversations
            .AsNoTracking()
            .Where(item => item.Id == conversationId)
            .Select(item => item.Farm.OwnerUserId)
            .SingleOrDefaultAsync(cancellationToken);
        if (ownerUserId is null)
            return NotFound();

        var result = await supportService.SendAdminMessageAsync(
            conversationId,
            AdminId(),
            request,
            cancellationToken);
        if (result.Status == SupportOperationStatus.Succeeded && result.Value is not null)
        {
            await hubContext.Clients.Group(SupportHub.UserGroup(ownerUserId))
                .SendAsync("SupportMessageReceived", result.Value, cancellationToken);
            await hubContext.Clients.Group(SupportHub.AdminGroup)
                .SendAsync("SupportMessageReceived", result.Value, cancellationToken);
        }

        return Map(result);
    }

    private ActionResult<T> Map<T>(SupportOperationResult<T> result) => result.Status switch
    {
        SupportOperationStatus.Succeeded => Ok(result.Value),
        SupportOperationStatus.NotFound => NotFound(),
        _ => BadRequest(result.ErrorMessage)
    };

    private string AdminId() =>
        User.FindFirstValue(ClaimTypes.NameIdentifier)
        ?? throw new InvalidOperationException("An authenticated user id is required.");
}
