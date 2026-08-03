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

        var registrationCounts = await context.Farms.AsNoTracking()
            .GroupBy(farm => new { farm.CreatedAt.Year, farm.CreatedAt.Month })
            .Select(group => new
            {
                group.Key.Year,
                group.Key.Month,
                Count = group.Count()
            })
            .OrderBy(item => item.Year)
            .ThenBy(item => item.Month)
            .ToListAsync(cancellationToken);
        var registrations = registrationCounts
            .Select(item => new NamedCountDto($"{item.Year:D4}-{item.Month:D2}", item.Count))
            .ToList();

        var planStatusCounts = await context.ExportPlans.AsNoTracking()
            .GroupBy(plan => plan.Status)
            .Select(group => new { Status = group.Key, Count = group.Count() })
            .ToListAsync(cancellationToken);
        var plansByStatus = planStatusCounts
            .Select(item => new NamedCountDto(item.Status.ToString(), item.Count))
            .ToList();

        var taskStatusCounts = await context.FarmTasks.AsNoTracking()
            .GroupBy(task => task.Status)
            .Select(group => new { Status = group.Key, Count = group.Count() })
            .ToListAsync(cancellationToken);
        var tasksByStatus = taskStatusCounts
            .Select(item => new NamedCountDto(item.Status.ToString(), item.Count))
            .ToList();

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
            exportPlansByDestination,
            registrations,
            plansByStatus,
            tasksByStatus);
    }
}
