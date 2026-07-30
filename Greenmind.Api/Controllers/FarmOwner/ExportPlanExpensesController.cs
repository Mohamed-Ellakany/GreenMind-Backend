using System.Security.Claims;
using Greenmind.Application.Contracts.Api;
using Greenmind.Application.Contracts.Costs;
using Greenmind.Application.Dtos.Costs;
using Greenmind.Domain.Consts;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Greenmind.Api.Controllers.FarmOwner;

[ApiController]
[Route("api/export-plans/{exportPlanId:int}/expenses")]
[Authorize(Roles = RoleNames.FarmOwner)]
public sealed class ExportPlanExpensesController(
    ICostManagementService costService) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<ExpensePageDto>> List(
        int exportPlanId,
        [FromQuery] ExpenseFilters filters,
        CancellationToken cancellationToken)
    {
        var result = await costService.ListExpensesAsync(
            exportPlanId,
            GetOwnerUserId(),
            filters,
            cancellationToken);
        return result.Status == CostOperationStatus.NotFound
            ? NotFound()
            : Ok(result.Value);
    }

    [HttpPost]
    public async Task<ActionResult<ExpenseDto>> Create(
        int exportPlanId,
        [FromBody] ExpenseRequest request,
        CancellationToken cancellationToken)
    {
        var result = await costService.CreateExpenseAsync(
            exportPlanId,
            GetOwnerUserId(),
            request,
            cancellationToken);
        return result.Status == CostOperationStatus.Succeeded
            ? StatusCode(StatusCodes.Status201Created, result.Value)
            : Map(result);
    }

    [HttpGet("export.csv")]
    [Produces("text/csv")]
    public async Task<IActionResult> Export(
        int exportPlanId,
        [FromQuery] ExpenseFilters filters,
        CancellationToken cancellationToken)
    {
        var result = await costService.ExportExpensesAsync(
            exportPlanId,
            GetOwnerUserId(),
            filters,
            cancellationToken);
        return result.Status == CostOperationStatus.NotFound
            ? NotFound()
            : File(
                result.Value!.Content,
                "text/csv; charset=utf-8",
                result.Value.FileName);
    }

    [HttpGet("{expenseId:int}")]
    public async Task<ActionResult<ExpenseDto>> Get(
        int exportPlanId,
        int expenseId,
        CancellationToken cancellationToken)
    {
        var result = await costService.GetExpenseAsync(
            exportPlanId,
            expenseId,
            GetOwnerUserId(),
            cancellationToken);
        return Map(result);
    }

    [HttpPut("{expenseId:int}")]
    public async Task<ActionResult<ExpenseDto>> Update(
        int exportPlanId,
        int expenseId,
        [FromBody] ExpenseRequest request,
        CancellationToken cancellationToken)
    {
        var result = await costService.UpdateExpenseAsync(
            exportPlanId,
            expenseId,
            GetOwnerUserId(),
            request,
            cancellationToken);
        return Map(result);
    }

    [HttpDelete("{expenseId:int}")]
    public async Task<IActionResult> Delete(
        int exportPlanId,
        int expenseId,
        CancellationToken cancellationToken)
    {
        var result = await costService.DeleteExpenseAsync(
            exportPlanId,
            expenseId,
            GetOwnerUserId(),
            cancellationToken);
        return result.Status == CostOperationStatus.Succeeded
            ? NoContent()
            : NotFound();
    }

    private ActionResult<ExpenseDto> Map(CostOperationResult<ExpenseDto> result) =>
        result.Status switch
        {
            CostOperationStatus.Succeeded => Ok(result.Value),
            CostOperationStatus.NotFound => NotFound(),
            _ => BadRequest(ApiResponseFactory.Failure<ExpenseDto>(
                StatusCodes.Status400BadRequest,
                result.ErrorCode ?? ApiErrorCodes.ValidationFailed,
                result.ErrorMessage ?? "تعذر حفظ المصروف.",
                HttpContext.TraceIdentifier))
        };

    private string GetOwnerUserId() =>
        User.FindFirstValue(ClaimTypes.NameIdentifier)
        ?? throw new InvalidOperationException("An authenticated user id is required.");
}
