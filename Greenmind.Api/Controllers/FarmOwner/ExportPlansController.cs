using System.Security.Claims;
using Greenmind.Application.Contracts.Api;
using Greenmind.Application.Contracts.ExportPlans;
using Greenmind.Application.Dtos.ExportPlans;
using Greenmind.Domain.Consts;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Greenmind.Api.Controllers.FarmOwner;

[ApiController]
[Route("api/farm-owner/export-plans")]
[Authorize(Roles = RoleNames.FarmOwner)]
public sealed class ExportPlansController(IExportPlanService exportPlanService) : ControllerBase
{
    [HttpPost]
    public async Task<ActionResult<ExportPlanDetailsDto>> Create(
        [FromBody] ExportPlanCreateRequest request,
        CancellationToken cancellationToken)
    {
        var result = await exportPlanService.CreateAsync(
            GetOwnerUserId(),
            request,
            cancellationToken);

        return result.Status switch
        {
            ExportPlanResultStatus.Succeeded => CreatedAtAction(
                nameof(GetById),
                new { exportPlanId = result.Value!.Id },
                result.Value),
            ExportPlanResultStatus.InvalidReference => BadRequest(
                ApiResponseFactory.ValidationFailure<ExportPlanDetailsDto>(
                    new Dictionary<string, string[]>
                    {
                        [result.ErrorField!] = [result.ErrorMessage!]
                    },
                    HttpContext.TraceIdentifier)),
            _ => NotFound()
        };
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<ExportPlanListItemDto>>> GetAll(
        CancellationToken cancellationToken) =>
        Ok(await exportPlanService.GetAllAsync(GetOwnerUserId(), cancellationToken));

    [HttpGet("{exportPlanId:int}")]
    public async Task<ActionResult<ExportPlanDetailsDto>> GetById(
        int exportPlanId,
        CancellationToken cancellationToken)
    {
        var result = await exportPlanService.GetByIdAsync(
            exportPlanId,
            GetOwnerUserId(),
            cancellationToken);

        return result.Status == ExportPlanResultStatus.Succeeded
            ? Ok(result.Value)
            : NotFound();
    }

    [HttpGet("{exportPlanId:int}/planning-status")]
    public async Task<ActionResult<PlanningStatusDto>> GetPlanningStatus(
        int exportPlanId,
        CancellationToken cancellationToken)
    {
        var result = await exportPlanService.GetPlanningStatusAsync(
            exportPlanId,
            GetOwnerUserId(),
            cancellationToken);

        return result.Status == ExportPlanResultStatus.Succeeded
            ? Ok(result.Value)
            : NotFound();
    }

    [HttpPost("{exportPlanId:int}/planning/retry")]
    public async Task<ActionResult<PlanningStatusDto>> RetryPlanning(
        int exportPlanId,
        CancellationToken cancellationToken)
    {
        var result = await exportPlanService.RetryPlanningAsync(
            exportPlanId,
            GetOwnerUserId(),
            cancellationToken);

        return result.Status switch
        {
            ExportPlanResultStatus.Succeeded => Accepted(result.Value),
            ExportPlanResultStatus.InvalidReference => Conflict(result.ErrorMessage),
            _ => NotFound()
        };
    }

    private string GetOwnerUserId() =>
        User.FindFirstValue(ClaimTypes.NameIdentifier)
        ?? throw new InvalidOperationException("An authenticated user id is required.");
}
