using System.Security.Claims;
using Greenmind.Application.Contracts.Costs;
using Greenmind.Application.Dtos.Costs;
using Greenmind.Domain.Consts;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Greenmind.Api.Controllers.FarmOwner;

[ApiController]
[Authorize(Roles = RoleNames.FarmOwner)]
public sealed class CostAnalyticsController(
    ICostManagementService costService) : ControllerBase
{
    [HttpGet("api/export-plans/{exportPlanId:int}/cost-dashboard")]
    public async Task<ActionResult<CostDashboardDto>> GetDashboard(
        int exportPlanId,
        CancellationToken cancellationToken)
    {
        var result = await costService.GetCostDashboardAsync(
            exportPlanId,
            GetOwnerUserId(),
            cancellationToken);
        return result.Status == CostOperationStatus.NotFound
            ? NotFound()
            : Ok(result.Value);
    }

    [HttpGet("api/farm/export-plan-cost-comparison")]
    public async Task<ActionResult<IReadOnlyList<ExportPlanCostComparisonItemDto>>> Compare(
        CancellationToken cancellationToken)
    {
        var result = await costService.CompareExportPlansAsync(
            GetOwnerUserId(),
            cancellationToken);
        return Ok(result.Value);
    }

    private string GetOwnerUserId() =>
        User.FindFirstValue(ClaimTypes.NameIdentifier)
        ?? throw new InvalidOperationException("An authenticated user id is required.");
}
