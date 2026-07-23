using Greenmind.Application.Contracts.ExportPlans;
using Greenmind.Application.Dtos.ExportPlans;
using Greenmind.Domain.Entities;
using Greenmind.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Greenmind.Infrastructure.ExportPlans;

public sealed class ExportPlanService(GreenmindDbContext dbContext) : IExportPlanService
{
    public async Task<ExportPlanResult<ExportPlanDetailsDto>> CreateAsync(
        string ownerUserId,
        ExportPlanCreateRequest request,
        CancellationToken cancellationToken = default)
    {
        var cropId = request.CropId.GetValueOrDefault();
        var destinationId = request.DestinationId.GetValueOrDefault();

        var farmId = await dbContext.Farms
            .AsNoTracking()
            .Where(farm => farm.OwnerUserId == ownerUserId)
            .Select(farm => (int?)farm.Id)
            .SingleOrDefaultAsync(cancellationToken);

        if (!farmId.HasValue)
        {
            return ExportPlanResult<ExportPlanDetailsDto>.NotFound();
        }

        if (!await dbContext.Crops.AnyAsync(
                crop => crop.Id == cropId && crop.IsActive,
                cancellationToken))
        {
            return ExportPlanResult<ExportPlanDetailsDto>.InvalidReference(
                nameof(request.CropId),
                "Crop must refer to an active record.");
        }

        if (!await dbContext.Destinations.AnyAsync(
                destination => destination.Id == destinationId && destination.IsActive,
                cancellationToken))
        {
            return ExportPlanResult<ExportPlanDetailsDto>.InvalidReference(
                nameof(request.DestinationId),
                "Destination must refer to an active record.");
        }

        var exportPlan = new ExportPlan
        {
            FarmId = farmId.Value,
            CropId = cropId,
            DestinationId = destinationId,
            Name = request.Name!.Trim(),
            Governorate = request.Governorate!.Trim(),
            City = request.City!.Trim(),
            Latitude = request.Latitude!.Value,
            Longitude = request.Longitude!.Value,
            AreaInFeddan = request.AreaInFeddan!.Value,
            PlantingDate = request.PlantingDate!.Value,
            ExpectedHarvestDate = request.ExpectedHarvestDate!.Value,
            ExpectedExportDate = request.ExpectedExportDate!.Value,
            Status = request.Status!.Value,
            Notes = NormalizeNotes(request.Notes)
        };

        dbContext.ExportPlans.Add(exportPlan);
        await dbContext.SaveChangesAsync(cancellationToken);

        return ExportPlanResult<ExportPlanDetailsDto>.Success(await MapDetailsAsync(exportPlan.Id, cancellationToken));
    }

    public async Task<IReadOnlyList<ExportPlanListItemDto>> GetAllAsync(
        string ownerUserId,
        CancellationToken cancellationToken = default) =>
        await dbContext.ExportPlans
            .AsNoTracking()
            .Where(plan => plan.Farm.OwnerUserId == ownerUserId)
            .OrderBy(plan => plan.ExpectedExportDate)
            .ThenBy(plan => plan.Id)
            .Select(plan => new ExportPlanListItemDto(
                plan.Id,
                plan.Name,
                plan.Crop.Name,
                plan.Destination.Name,
                plan.PlantingDate,
                plan.ExpectedHarvestDate,
                plan.ExpectedExportDate,
                plan.Status.ToString()))
            .ToListAsync(cancellationToken);

    public async Task<ExportPlanResult<ExportPlanDetailsDto>> GetByIdAsync(
        int exportPlanId,
        string ownerUserId,
        CancellationToken cancellationToken = default)
    {
        var plan = await dbContext.ExportPlans
            .AsNoTracking()
            .Where(item => item.Id == exportPlanId && item.Farm.OwnerUserId == ownerUserId)
            .Select(item => new ExportPlanDetailsDto(
                item.Id,
                item.Name,
                item.CropId,
                item.Crop.Name,
                item.DestinationId,
                item.Destination.Name,
                item.Governorate,
                item.City,
                item.Latitude,
                item.Longitude,
                item.AreaInFeddan,
                item.PlantingDate,
                item.ExpectedHarvestDate,
                item.ExpectedExportDate,
                item.Status.ToString(),
                item.Notes))
            .SingleOrDefaultAsync(cancellationToken);

        return plan is null
            ? ExportPlanResult<ExportPlanDetailsDto>.NotFound()
            : ExportPlanResult<ExportPlanDetailsDto>.Success(plan);
    }

    private async Task<ExportPlanDetailsDto> MapDetailsAsync(int exportPlanId, CancellationToken cancellationToken) =>
        (await dbContext.ExportPlans
            .AsNoTracking()
            .Where(plan => plan.Id == exportPlanId)
            .Select(plan => new ExportPlanDetailsDto(
                plan.Id,
                plan.Name,
                plan.CropId,
                plan.Crop.Name,
                plan.DestinationId,
                plan.Destination.Name,
                plan.Governorate,
                plan.City,
                plan.Latitude,
                plan.Longitude,
                plan.AreaInFeddan,
                plan.PlantingDate,
                plan.ExpectedHarvestDate,
                plan.ExpectedExportDate,
                plan.Status.ToString(),
                plan.Notes))
            .SingleAsync(cancellationToken));

    private static string? NormalizeNotes(string? notes) =>
        string.IsNullOrWhiteSpace(notes) ? null : notes.Trim();
}
