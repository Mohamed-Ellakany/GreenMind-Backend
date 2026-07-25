using System.Security.Claims;
using Greenmind.Application.Contracts.Soil;
using Greenmind.Application.Dtos.Soil;
using Greenmind.Domain.Consts;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Greenmind.Api.Controllers.FarmOwner;

[ApiController]
[Route("api/farm-owner/export-plans/{exportPlanId:int}/soil-measurements")]
[Authorize(Roles = RoleNames.FarmOwner)]
public sealed class SoilMeasurementsController(
    ISoilMeasurementService soilMeasurementService) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<SoilMeasurementDto>>> GetAll(
        int exportPlanId,
        CancellationToken cancellationToken)
    {
        var result = await soilMeasurementService.GetAllAsync(
            exportPlanId,
            GetOwnerUserId(),
            cancellationToken);

        return result.Found ? Ok(result.Value) : NotFound();
    }

    [HttpGet("latest")]
    public async Task<ActionResult<SoilMeasurementDto?>> GetLatest(
        int exportPlanId,
        CancellationToken cancellationToken)
    {
        var result = await soilMeasurementService.GetLatestAsync(
            exportPlanId,
            GetOwnerUserId(),
            cancellationToken);

        return result.Found ? Ok(result.Value) : NotFound();
    }

    [HttpPost]
    public async Task<ActionResult<SoilMeasurementDto>> Create(
        int exportPlanId,
        [FromBody] SoilMeasurementRequest request,
        CancellationToken cancellationToken)
    {
        var result = await soilMeasurementService.CreateAsync(
            exportPlanId,
            GetOwnerUserId(),
            request,
            cancellationToken);

        return result.Found
            ? StatusCode(StatusCodes.Status201Created, result.Value)
            : NotFound();
    }

    [HttpPut("{measurementId:int}")]
    public async Task<ActionResult<SoilMeasurementDto>> Update(
        int exportPlanId,
        int measurementId,
        [FromBody] SoilMeasurementRequest request,
        CancellationToken cancellationToken)
    {
        var result = await soilMeasurementService.UpdateAsync(
            exportPlanId,
            measurementId,
            GetOwnerUserId(),
            request,
            cancellationToken);

        return result.Found ? Ok(result.Value) : NotFound();
    }

    /// <summary>
    /// Permanently deletes a soil measurement because the current schema has no soft-delete column.
    /// </summary>
    [HttpDelete("{measurementId:int}")]
    public async Task<ActionResult<SoilMeasurementDeletedDto>> Delete(
        int exportPlanId,
        int measurementId,
        CancellationToken cancellationToken)
    {
        var result = await soilMeasurementService.DeleteAsync(
            exportPlanId,
            measurementId,
            GetOwnerUserId(),
            cancellationToken);

        return result.Found
            ? Ok(new SoilMeasurementDeletedDto(result.Value, DeletedPermanently: true))
            : NotFound();
    }

    private string GetOwnerUserId() =>
        User.FindFirstValue(ClaimTypes.NameIdentifier)
        ?? throw new InvalidOperationException("An authenticated user id is required.");
}

public sealed record SoilMeasurementDeletedDto(int Id, bool DeletedPermanently);
