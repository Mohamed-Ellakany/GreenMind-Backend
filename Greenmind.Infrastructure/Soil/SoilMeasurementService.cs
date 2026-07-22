using Greenmind.Application.Contracts.Soil;
using Greenmind.Application.Dtos.Soil;
using Greenmind.Domain.Entities;
using Greenmind.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Greenmind.Infrastructure.Soil;

public sealed class SoilMeasurementService(GreenmindDbContext dbContext) : ISoilMeasurementService
{
    public async Task<SoilMeasurementResult<IReadOnlyList<SoilMeasurementDto>>> GetAllAsync(
        int exportPlanId,
        string ownerUserId,
        CancellationToken cancellationToken = default)
    {
        if (!await IsOwnedExportPlanAsync(exportPlanId, ownerUserId, cancellationToken))
        {
            return SoilMeasurementResult<IReadOnlyList<SoilMeasurementDto>>.NotFound();
        }

        var measurements = await dbContext.SoilMeasurements
            .AsNoTracking()
            .Where(measurement => measurement.ExportPlanId == exportPlanId)
            .OrderByDescending(measurement => measurement.MeasurementDate)
            .ThenByDescending(measurement => measurement.CreatedAt)
            .Select(measurement => new SoilMeasurementDto(
                measurement.Id,
                measurement.MeasurementDate,
                measurement.PH,
                measurement.EC,
                measurement.Nitrogen,
                measurement.Phosphorus,
                measurement.Potassium,
                measurement.Moisture,
                measurement.Notes,
                measurement.CreatedAt))
            .ToListAsync(cancellationToken);

        return SoilMeasurementResult<IReadOnlyList<SoilMeasurementDto>>.Success(measurements);
    }

    public async Task<SoilMeasurementResult<SoilMeasurementDto?>> GetLatestAsync(
        int exportPlanId,
        string ownerUserId,
        CancellationToken cancellationToken = default)
    {
        if (!await IsOwnedExportPlanAsync(exportPlanId, ownerUserId, cancellationToken))
        {
            return SoilMeasurementResult<SoilMeasurementDto?>.NotFound();
        }

        var measurement = await dbContext.SoilMeasurements
            .AsNoTracking()
            .Where(item => item.ExportPlanId == exportPlanId)
            .OrderByDescending(item => item.MeasurementDate)
            .ThenByDescending(item => item.CreatedAt)
            .Select(item => new SoilMeasurementDto(
                item.Id,
                item.MeasurementDate,
                item.PH,
                item.EC,
                item.Nitrogen,
                item.Phosphorus,
                item.Potassium,
                item.Moisture,
                item.Notes,
                item.CreatedAt))
            .FirstOrDefaultAsync(cancellationToken);

        return SoilMeasurementResult<SoilMeasurementDto?>.Success(measurement);
    }

    public async Task<SoilMeasurementResult<SoilMeasurementDto>> CreateAsync(
        int exportPlanId,
        string ownerUserId,
        SoilMeasurementRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!await IsOwnedExportPlanAsync(exportPlanId, ownerUserId, cancellationToken))
        {
            return SoilMeasurementResult<SoilMeasurementDto>.NotFound();
        }

        var measurement = new SoilMeasurement
        {
            ExportPlanId = exportPlanId,
            MeasurementDate = request.MeasurementDate!.Value,
            PH = request.Ph!.Value,
            EC = request.Ec!.Value,
            Nitrogen = request.Nitrogen!.Value,
            Phosphorus = request.Phosphorus!.Value,
            Potassium = request.Potassium!.Value,
            Moisture = request.Moisture!.Value,
            Notes = NormalizeNotes(request.Notes)
        };

        await dbContext.SoilMeasurements.AddAsync(measurement, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);

        return SoilMeasurementResult<SoilMeasurementDto>.Success(Map(measurement));
    }

    public async Task<SoilMeasurementResult<SoilMeasurementDto>> UpdateAsync(
        int exportPlanId,
        int measurementId,
        string ownerUserId,
        SoilMeasurementRequest request,
        CancellationToken cancellationToken = default)
    {
        var measurement = await FindOwnedMeasurementAsync(
            exportPlanId,
            measurementId,
            ownerUserId,
            cancellationToken);

        if (measurement is null)
        {
            return SoilMeasurementResult<SoilMeasurementDto>.NotFound();
        }

        measurement.MeasurementDate = request.MeasurementDate!.Value;
        measurement.PH = request.Ph!.Value;
        measurement.EC = request.Ec!.Value;
        measurement.Nitrogen = request.Nitrogen!.Value;
        measurement.Phosphorus = request.Phosphorus!.Value;
        measurement.Potassium = request.Potassium!.Value;
        measurement.Moisture = request.Moisture!.Value;
        measurement.Notes = NormalizeNotes(request.Notes);

        await dbContext.SaveChangesAsync(cancellationToken);
        return SoilMeasurementResult<SoilMeasurementDto>.Success(Map(measurement));
    }

    public async Task<SoilMeasurementResult<int>> DeleteAsync(
        int exportPlanId,
        int measurementId,
        string ownerUserId,
        CancellationToken cancellationToken = default)
    {
        var measurement = await FindOwnedMeasurementAsync(
            exportPlanId,
            measurementId,
            ownerUserId,
            cancellationToken);

        if (measurement is null)
        {
            return SoilMeasurementResult<int>.NotFound();
        }

        dbContext.SoilMeasurements.Remove(measurement);
        await dbContext.SaveChangesAsync(cancellationToken);

        return SoilMeasurementResult<int>.Success(measurementId);
    }

    private Task<bool> IsOwnedExportPlanAsync(
        int exportPlanId,
        string ownerUserId,
        CancellationToken cancellationToken) =>
        dbContext.ExportPlans
            .AsNoTracking()
            .AnyAsync(
                plan => plan.Id == exportPlanId && plan.Farm.OwnerUserId == ownerUserId,
                cancellationToken);

    private Task<SoilMeasurement?> FindOwnedMeasurementAsync(
        int exportPlanId,
        int measurementId,
        string ownerUserId,
        CancellationToken cancellationToken) =>
        dbContext.SoilMeasurements
            .Where(measurement =>
                measurement.Id == measurementId &&
                measurement.ExportPlanId == exportPlanId &&
                measurement.ExportPlan.Farm.OwnerUserId == ownerUserId)
            .SingleOrDefaultAsync(cancellationToken);

    private static SoilMeasurementDto Map(SoilMeasurement measurement) =>
        new(
            measurement.Id,
            measurement.MeasurementDate,
            measurement.PH,
            measurement.EC,
            measurement.Nitrogen,
            measurement.Phosphorus,
            measurement.Potassium,
            measurement.Moisture,
            measurement.Notes,
            measurement.CreatedAt);

    private static string? NormalizeNotes(string? notes) =>
        string.IsNullOrWhiteSpace(notes) ? null : notes.Trim();
}
