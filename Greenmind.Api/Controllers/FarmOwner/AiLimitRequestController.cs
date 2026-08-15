using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using Greenmind.Application.Contracts.Admin;
using Greenmind.Application.Dtos.Admin;
using Greenmind.Domain.Consts;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Greenmind.Api.Controllers.FarmOwner;

[ApiController]
[Route("api/farm")]
[Authorize(Roles = RoleNames.FarmOwner)]
public sealed class AiLimitRequestController(IAiLimitRequestService service) : ControllerBase
{
    [HttpGet("ai-budget")]
    public async Task<ActionResult<UserBudgetStatusDto>> GetBudgetStatus(
        CancellationToken cancellationToken)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? throw new InvalidOperationException("An authenticated user id is required.");
        return Ok(await service.GetUserBudgetStatusAsync(userId, cancellationToken));
    }

    [HttpPost("ai-limit-requests")]
    public async Task<IActionResult> SubmitRequest(
        [FromBody] SubmitAiLimitRequest request,
        CancellationToken cancellationToken)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? throw new InvalidOperationException("An authenticated user id is required.");

        var created = await service.SubmitRequestAsync(
            userId,
            request.ContactName!,
            request.ContactPhone!,
            cancellationToken);

        return created
            ? StatusCode(StatusCodes.Status201Created)
            : Conflict(new { message = "لديك طلب معلّق بالفعل." });
    }
}

public sealed class SubmitAiLimitRequest
{
    [Required, StringLength(200, MinimumLength = 2)]
    public string? ContactName { get; init; }

    [Required, StringLength(30, MinimumLength = 6)]
    public string? ContactPhone { get; init; }
}
