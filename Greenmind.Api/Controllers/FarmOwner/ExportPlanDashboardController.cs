using System.Security.Claims;
using Greenmind.Application.Contracts.Dashboard;
using Greenmind.Application.Dtos.Dashboard;
using Greenmind.Domain.Consts;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Greenmind.Api.Controllers.FarmOwner;

[ApiController]
[Route("api/farm-owner/export-plans")]
[Authorize(Roles = RoleNames.FarmOwner)]
public sealed class ExportPlanDashboardController(
    IExportPlanDashboardService dashboardService) : ControllerBase
{
    [HttpGet("{exportPlanId:int}/dashboard")]
    public async Task<ActionResult<ExportPlanDashboardDto>> Get(
        int exportPlanId,
        CancellationToken cancellationToken)
    {
        var ownerUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(ownerUserId))
        {
            return Unauthorized();
        }

        var dashboard = await dashboardService.GetAsync(
            exportPlanId,
            ownerUserId,
            cancellationToken);

        return dashboard is null ? NotFound() : Ok(dashboard);
    }
}
