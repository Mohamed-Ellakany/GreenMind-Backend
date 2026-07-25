using Greenmind.Application.Contracts.Admin;
using Greenmind.Application.Dtos.Admin;
using Greenmind.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Greenmind.Infrastructure.Admin;

public sealed class PlatformStatisticsService(GreenmindDbContext context) : IPlatformStatisticsService
{
    public async Task<PlatformStatisticsDto> GetAsync(CancellationToken cancellationToken = default)
    {
        var totalFarms = await context.Farms.CountAsync(cancellationToken);
        var pendingFarms = await context.Farms.CountAsync(farm => !farm.IsApproved, cancellationToken);
        var activeFarms = await context.Farms.CountAsync(
            farm => farm.IsApproved && farm.Owner.IsActive,
            cancellationToken);
        var inactiveFarms = totalFarms - activeFarms;

        var exportPlansByCrop = await context.ExportPlans
            .AsNoTracking()
            .GroupBy(plan => new { plan.CropId, plan.Crop.Name })
            .Select(group => new
            {
                Id = group.Key.CropId,
                Name = group.Key.Name,
                ExportPlanCount = group.Count()
            })
            .OrderByDescending(item => item.ExportPlanCount)
            .ThenBy(item => item.Name)
            .Select(item => new ExportPlanBreakdownDto(
                item.Id,
                item.Name,
                item.ExportPlanCount))
            .ToListAsync(cancellationToken);

        var exportPlansByDestination = await context.ExportPlans
            .AsNoTracking()
            .GroupBy(plan => new { plan.DestinationId, plan.Destination.Name })
            .Select(group => new
            {
                Id = group.Key.DestinationId,
                Name = group.Key.Name,
                ExportPlanCount = group.Count()
            })
            .OrderByDescending(item => item.ExportPlanCount)
            .ThenBy(item => item.Name)
            .Select(item => new ExportPlanBreakdownDto(
                item.Id,
                item.Name,
                item.ExportPlanCount))
            .ToListAsync(cancellationToken);

        return new PlatformStatisticsDto(
            totalFarms,
            pendingFarms,
            activeFarms,
            inactiveFarms,
            await context.Crops.CountAsync(cancellationToken),
            await context.Destinations.CountAsync(cancellationToken),
            await context.ExportPlans.CountAsync(cancellationToken),
            await context.FarmTasks.CountAsync(cancellationToken),
            exportPlansByCrop,
            exportPlansByDestination);
    }
}
