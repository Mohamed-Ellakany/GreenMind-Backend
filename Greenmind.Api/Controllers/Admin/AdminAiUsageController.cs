using System.ComponentModel.DataAnnotations;
using Greenmind.Application.Contracts.Admin;
using Greenmind.Application.Dtos.Admin;
using Greenmind.Domain.Consts;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Greenmind.Api.Controllers.Admin;

[ApiController]
[Route("api/admin/ai-usage")]
[Authorize(Roles = RoleNames.Admin)]
public sealed class AdminAiUsageController(IAiUsageAdminService service) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<AdminAiUsageOverviewDto>> GetOverview(
        [FromQuery] DateTime? from = null,
        [FromQuery] DateTime? to = null,
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        if (from.HasValue && to.HasValue && from.Value.Date > to.Value.Date)
            return BadRequest("The from date must be before or equal to the to date.");

        return Ok(await service.GetOverviewAsync(
            from, to, pageNumber, pageSize, cancellationToken));
    }
}

[ApiController]
[Route("api/admin/users")]
[Authorize(Roles = RoleNames.Admin)]
public sealed class AdminUserDetailsController(IAiUsageAdminService service) : ControllerBase
{
    [HttpGet("{userId}")]
    public async Task<ActionResult<AdminUserDetailsDto>> GetUser(
        string userId,
        [FromQuery] DateTime? from = null,
        [FromQuery] DateTime? to = null,
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        if (from.HasValue && to.HasValue && from.Value.Date > to.Value.Date)
            return BadRequest("The from date must be before or equal to the to date.");

        var result = await service.GetUserDetailsAsync(
            userId, from, to, pageNumber, pageSize, cancellationToken);
        return result is null ? NotFound() : Ok(result);
    }

    [HttpPatch("{userId}/ai-access")]
    public async Task<IActionResult> SetAiAccess(
        string userId,
        [FromBody] SetAiAccessRequest request,
        CancellationToken cancellationToken)
    {
        var updated = await service.SetAiAccessSuspendedAsync(
            userId, request.IsSuspended!.Value, cancellationToken);
        return updated ? NoContent() : NotFound();
    }
}

public sealed class SetAiAccessRequest
{
    [Required]
    public bool? IsSuspended { get; init; }
}
