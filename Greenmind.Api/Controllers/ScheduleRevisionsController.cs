using Greenmind.Application.Contracts.Planning;
using Greenmind.Application.Dtos.Planning;
using Greenmind.Domain.Consts;
using Greenmind.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace Greenmind.Api.Controllers;

[ApiController]
[Route("api/export-plans/{exportPlanId:int}/schedule-revision")]
[Authorize(Roles = RoleNames.FarmOwner + "," + RoleNames.Admin)]
public sealed class ScheduleRevisionsController(
    GreenmindDbContext db,
    IScheduleRevisionService revisions) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<ScheduleRevisionDto>> Get(int exportPlanId, CancellationToken cancellationToken)
    {
        if (!await OwnsPlanAsync(exportPlanId, cancellationToken)) return NotFound();
        var revision = await revisions.GetLatestAsync(exportPlanId, cancellationToken);
        return revision is null ? NotFound() : Ok(revision);
    }

    [HttpPost("retry")]
    public async Task<ActionResult<ScheduleRevisionDto>> Retry(int exportPlanId, CancellationToken cancellationToken)
    {
        if (!await OwnsPlanAsync(exportPlanId, cancellationToken)) return NotFound();
        var revision = await revisions.QueueAsync(exportPlanId, "UserRetry", cancellationToken);
        return revision is null ? NotFound() : AcceptedAtAction(nameof(Get), new { exportPlanId }, revision);
    }

    private async Task<bool> OwnsPlanAsync(int exportPlanId, CancellationToken cancellationToken)
    {
        if (User.IsInRole(RoleNames.Admin)) return await db.ExportPlans.AnyAsync(plan => plan.Id == exportPlanId, cancellationToken);
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return !string.IsNullOrWhiteSpace(userId) && await db.ExportPlans.AnyAsync(
            plan => plan.Id == exportPlanId && plan.Farm.OwnerUserId == userId, cancellationToken);
    }
}
