using System.Security.Claims;
using Greenmind.Api.Hubs;
using Greenmind.Application.Contracts.Support;
using Greenmind.Application.Dtos.Support;
using Greenmind.Domain.Consts;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;

namespace Greenmind.Api.Controllers.FarmOwner;

[ApiController]
[Route("api/support/conversation")]
[Authorize(Roles = RoleNames.FarmOwner)]
public sealed class SupportController(
    ISupportService supportService,
    IHubContext<SupportHub> hubContext) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<SupportConversationDto>> GetConversation(
        CancellationToken cancellationToken) =>
        Map(await supportService.GetOrCreateCustomerConversationAsync(
            OwnerId(),
            cancellationToken));

    [HttpGet("messages")]
    public async Task<ActionResult<SupportMessagePageDto>> GetMessages(
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 100,
        CancellationToken cancellationToken = default) =>
        Map(await supportService.GetCustomerMessagesAsync(
            OwnerId(),
            pageNumber,
            pageSize,
            cancellationToken));

    [HttpPost("messages")]
    public async Task<ActionResult<SupportMessageDto>> SendMessage(
        [FromBody] SendSupportMessageRequest request,
        CancellationToken cancellationToken)
    {
        var result = await supportService.SendCustomerMessageAsync(
            OwnerId(),
            request,
            cancellationToken);
        if (result.Status == SupportOperationStatus.Succeeded && result.Value is not null)
        {
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

    private string OwnerId() =>
        User.FindFirstValue(ClaimTypes.NameIdentifier)
        ?? throw new InvalidOperationException("An authenticated user id is required.");
}
