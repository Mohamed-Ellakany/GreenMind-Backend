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
            Status = Greenmind.Domain.Consts.Enums.ExportPlanStatus.Planning,
            Notes = NormalizeNotes(request.Notes),
            PlanningStatus = Greenmind.Domain.Consts.Enums.PlanningGenerationStatus.Pending,
            PlanningRequestedAtUtc = DateTime.UtcNow,
            NextPlanningAttemptAtUtc = DateTime.UtcNow
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
                plan.Status.ToString(),
                plan.PlanningStatus.ToString(),
                plan.GeneratedTaskCount))
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
                item.Notes,
                item.PlanningStatus.ToString(),
                item.GeneratedTaskCount,
                item.PlanningFailureMessage))
            .SingleOrDefaultAsync(cancellationToken);

        return plan is null
            ? ExportPlanResult<ExportPlanDetailsDto>.NotFound()
            : ExportPlanResult<ExportPlanDetailsDto>.Success(plan);
    }

    public async Task<ExportPlanResult<PlanningStatusDto>> GetPlanningStatusAsync(
        int exportPlanId,
        string ownerUserId,
        CancellationToken cancellationToken = default)
    {
        var plan = await dbContext.ExportPlans
            .AsNoTracking()
            .Where(plan => plan.Id == exportPlanId && plan.Farm.OwnerUserId == ownerUserId)
            .SingleOrDefaultAsync(cancellationToken);

        return plan is null
            ? ExportPlanResult<PlanningStatusDto>.NotFound()
            : ExportPlanResult<PlanningStatusDto>.Success(MapPlanningStatus(plan));
    }

    public async Task<ExportPlanResult<PlanningStatusDto>> RetryPlanningAsync(
        int exportPlanId,
        string ownerUserId,
        CancellationToken cancellationToken = default)
    {
        var plan = await dbContext.ExportPlans
            .SingleOrDefaultAsync(item =>
                item.Id == exportPlanId && item.Farm.OwnerUserId == ownerUserId,
                cancellationToken);

        if (plan is null)
        {
            return ExportPlanResult<PlanningStatusDto>.NotFound();
        }

        if (plan.PlanningStatus != Greenmind.Domain.Consts.Enums.PlanningGenerationStatus.Failed)
        {
            return ExportPlanResult<PlanningStatusDto>.InvalidReference(
                nameof(plan.PlanningStatus),
                "Only failed planning jobs can be retried.");
        }

        plan.PlanningStatus = Greenmind.Domain.Consts.Enums.PlanningGenerationStatus.Pending;
        plan.PlanningRequestedAtUtc = DateTime.UtcNow;
        plan.PlanningStartedAtUtc = null;
        plan.PlanningCompletedAtUtc = null;
        plan.NextPlanningAttemptAtUtc = DateTime.UtcNow;
        plan.PlanningAttemptCount = 0;
        plan.PlanningFailureMessage = null;
        await dbContext.SaveChangesAsync(cancellationToken);

        return ExportPlanResult<PlanningStatusDto>.Success(MapPlanningStatus(plan));
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
                plan.Notes,
                plan.PlanningStatus.ToString(),
                plan.GeneratedTaskCount,
                plan.PlanningFailureMessage))
            .SingleAsync(cancellationToken));

    private static PlanningStatusDto MapPlanningStatus(ExportPlan plan) => new(
        plan.Id,
        plan.PlanningStatus.ToString(),
        plan.PlanningAttemptCount,
        plan.GeneratedTaskCount,
        plan.PlanningRequestedAtUtc,
        plan.PlanningStartedAtUtc,
        plan.PlanningCompletedAtUtc,
        plan.PlanningFailureMessage);

    private static string? NormalizeNotes(string? notes) =>
        string.IsNullOrWhiteSpace(notes) ? null : notes.Trim();
}
