using System.Security.Claims;
using Greenmind.Application.Contracts.Api;
using Greenmind.Application.Contracts.Costs;
using Greenmind.Application.Dtos.Costs;
using Greenmind.Domain.Consts;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Greenmind.Api.Controllers.FarmOwner;

[ApiController]
[Route("api/export-plans/{exportPlanId:int}/budget")]
[Authorize(Roles = RoleNames.FarmOwner)]
public sealed class ExportPlanBudgetController(
    ICostManagementService costService) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<BudgetDto?>> Get(
        int exportPlanId,
        CancellationToken cancellationToken)
    {
        var result = await costService.GetBudgetAsync(
            exportPlanId,
            GetOwnerUserId(),
            cancellationToken);
        return result.Status == CostOperationStatus.NotFound
            ? NotFound()
            : Ok(result.Value);
    }

    [HttpPut]
    public async Task<ActionResult<BudgetDto>> Put(
        int exportPlanId,
        [FromBody] BudgetRequest request,
        CancellationToken cancellationToken)
    {
        var result = await costService.UpsertBudgetAsync(
            exportPlanId,
            GetOwnerUserId(),
            request,
            cancellationToken);
        return Map(result);
    }

    private ActionResult<BudgetDto> Map(CostOperationResult<BudgetDto> result) =>
        result.Status switch
        {
            CostOperationStatus.Succeeded => Ok(result.Value),
            CostOperationStatus.NotFound => NotFound(),
            _ => BadRequest(ApiResponseFactory.Failure<BudgetDto>(
                StatusCodes.Status400BadRequest,
                result.ErrorCode ?? ApiErrorCodes.ValidationFailed,
                result.ErrorMessage ?? "تعذر حفظ الميزانية.",
                HttpContext.TraceIdentifier))
        };

    private string GetOwnerUserId() =>
        User.FindFirstValue(ClaimTypes.NameIdentifier)
        ?? throw new InvalidOperationException("An authenticated user id is required.");
}
