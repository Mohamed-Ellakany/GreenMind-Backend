using Greenmind.Application.Dtos.Soil;

namespace Greenmind.Application.Contracts.Soil;

public interface ISoilMeasurementService
{
    Task<SoilMeasurementResult<IReadOnlyList<SoilMeasurementDto>>> GetAllAsync(
        int exportPlanId,
        string ownerUserId,
        CancellationToken cancellationToken = default);

    Task<SoilMeasurementResult<SoilMeasurementDto?>> GetLatestAsync(
        int exportPlanId,
        string ownerUserId,
        CancellationToken cancellationToken = default);

    Task<SoilMeasurementResult<SoilMeasurementDto>> CreateAsync(
        int exportPlanId,
        string ownerUserId,
        SoilMeasurementRequest request,
        CancellationToken cancellationToken = default);

    Task<SoilMeasurementResult<SoilMeasurementDto>> UpdateAsync(
        int exportPlanId,
        int measurementId,
        string ownerUserId,
        SoilMeasurementRequest request,
        CancellationToken cancellationToken = default);

    Task<SoilMeasurementResult<int>> DeleteAsync(
        int exportPlanId,
        int measurementId,
        string ownerUserId,
        CancellationToken cancellationToken = default);
}
