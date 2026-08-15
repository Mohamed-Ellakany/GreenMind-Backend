using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using Greenmind.Application.Contracts.Admin;
using Greenmind.Application.Dtos.Admin;
using Greenmind.Domain.Consts;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Greenmind.Api.Controllers.Admin;

[ApiController]
[Route("api/admin/ai-limit-requests")]
[Authorize(Roles = RoleNames.Admin)]
public sealed class AdminAiLimitRequestController(IAiLimitRequestService service) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<AiLimitRequestDto>>> GetRequests(
        [FromQuery] bool pendingOnly = true,
        CancellationToken cancellationToken = default) =>
        Ok(await service.GetRequestsAsync(pendingOnly, cancellationToken));

    [HttpPatch("{requestId:int}")]
    public async Task<IActionResult> Resolve(
        int requestId,
        [FromBody] ResolveAiLimitRequestBody body,
        CancellationToken cancellationToken)
    {
        var adminUserId = User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? throw new InvalidOperationException("An authenticated admin user id is required.");

        var resolved = await service.ResolveRequestAsync(
            requestId,
            adminUserId,
            body.Approved!.Value,
            body.NewLimitUsd,
            body.AdminNote,
            cancellationToken);

        return resolved ? NoContent() : NotFound();
    }
}

public sealed class ResolveAiLimitRequestBody
{
    [Required]
    public bool? Approved { get; init; }

    public decimal? NewLimitUsd { get; init; }

    [StringLength(500)]
    public string? AdminNote { get; init; }
}
