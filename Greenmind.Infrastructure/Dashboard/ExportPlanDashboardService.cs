using Greenmind.Application.Contracts.Dashboard;
using Greenmind.Application.Dtos.Dashboard;
using Greenmind.Domain.Consts.Enums;
using Greenmind.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using DomainTaskStatus = Greenmind.Domain.Consts.Enums.TaskStatus;

namespace Greenmind.Infrastructure.Dashboard;

public sealed class ExportPlanDashboardService(
    GreenmindDbContext dbContext,
    ISeasonCalculationService seasonCalculationService,
    TimeProvider timeProvider) : IExportPlanDashboardService
{
    private const int DashboardTaskLimit = 5;

    public async Task<ExportPlanDashboardDto?> GetAsync(
        int exportPlanId,
        string ownerUserId,
        CancellationToken cancellationToken = default)
    {
        var plan = await dbContext.ExportPlans
            .AsNoTracking()
            .Where(item => item.Id == exportPlanId && item.Farm.OwnerUserId == ownerUserId)
            .Select(item => new ExportPlanProjection(
                item.Id,
                item.Name,
                item.Status,
                item.Crop.Name,
                item.Destination.Name,
                item.Governorate,
                item.City,
                item.AreaInFeddan,
                item.PlantingDate,
                item.ExpectedHarvestDate,
                item.ExpectedExportDate))
            .SingleOrDefaultAsync(cancellationToken);

        if (plan is null)
        {
            return null;
        }

        var today = DateOnly.FromDateTime(timeProvider.GetUtcNow().UtcDateTime);
        var latestSoilMeasurement = await dbContext.SoilMeasurements
            .AsNoTracking()
            .Where(measurement => measurement.ExportPlanId == exportPlanId)
            .OrderByDescending(measurement => measurement.MeasurementDate)
            .ThenByDescending(measurement => measurement.CreatedAt)
            .Select(measurement => new SoilMeasurementDashboardDto(
                measurement.MeasurementDate,
                measurement.PH,
                measurement.EC,
                measurement.Nitrogen,
                measurement.Phosphorus,
                measurement.Potassium,
                measurement.Moisture,
                measurement.Notes))
            .FirstOrDefaultAsync(cancellationToken);

        var taskSummary = await dbContext.FarmTasks
            .AsNoTracking()
            .Where(task => task.ExportPlanId == exportPlanId)
            .GroupBy(_ => 1)
            .Select(group => new TaskSummaryDto(
                group.Count(),
                group.Count(task => task.Status == DomainTaskStatus.Pending),
                group.Count(task => task.Status == DomainTaskStatus.InProgress),
                group.Count(task => task.Status == DomainTaskStatus.Completed),
                group.Count(task => task.Status == DomainTaskStatus.Cancelled)))
            .SingleOrDefaultAsync(cancellationToken)
            ?? new TaskSummaryDto(0, 0, 0, 0, 0);

        var upcomingTasks = await dbContext.FarmTasks
            .AsNoTracking()
            .Where(task =>
                task.ExportPlanId == exportPlanId &&
                (task.Status == DomainTaskStatus.Pending || task.Status == DomainTaskStatus.InProgress) &&
                task.PlannedDate >= today)
            .OrderBy(task => task.PlannedDate)
            .ThenByDescending(task => task.Priority)
            .Take(DashboardTaskLimit)
            .Select(task => new DashboardTaskProjection(
                task.Id,
                task.Title,
                task.TaskType.Name,
                task.PlannedDate,
                task.ActualDate,
                task.Status,
                task.Priority))
            .ToListAsync(cancellationToken);

        var overdueTasks = await dbContext.FarmTasks
            .AsNoTracking()
            .Where(task =>
                task.ExportPlanId == exportPlanId &&
                (task.Status == DomainTaskStatus.Pending || task.Status == DomainTaskStatus.InProgress) &&
                task.PlannedDate < today)
            .OrderByDescending(task => task.Priority)
            .ThenBy(task => task.PlannedDate)
            .Take(DashboardTaskLimit)
            .Select(task => new DashboardTaskProjection(
                task.Id,
                task.Title,
                task.TaskType.Name,
                task.PlannedDate,
                task.ActualDate,
                task.Status,
                task.Priority))
            .ToListAsync(cancellationToken);

        var completedTasks = await dbContext.FarmTasks
            .AsNoTracking()
            .Where(task => task.ExportPlanId == exportPlanId && task.Status == DomainTaskStatus.Completed)
            .OrderByDescending(task => task.ActualDate)
            .ThenByDescending(task => task.PlannedDate)
            .Take(DashboardTaskLimit)
            .Select(task => new DashboardTaskProjection(
                task.Id,
                task.Title,
                task.TaskType.Name,
                task.PlannedDate,
                task.ActualDate,
                task.Status,
                task.Priority))
            .ToListAsync(cancellationToken);

        return new ExportPlanDashboardDto(
            new ExportPlanDashboardOverviewDto(
                plan.Id,
                plan.Name,
                plan.Status.ToString(),
                plan.Crop,
                plan.Destination,
                plan.Governorate,
                plan.City,
                plan.AreaInFeddan,
                plan.PlantingDate,
                plan.ExpectedHarvestDate,
                plan.ExpectedExportDate),
            seasonCalculationService.Calculate(plan.PlantingDate, plan.ExpectedHarvestDate, today),
            latestSoilMeasurement,
            taskSummary,
            overdueTasks.Select(MapTask).ToList(),
            upcomingTasks.Select(MapTask).ToList(),
            completedTasks.Select(MapTask).ToList());
    }

    private static DashboardTaskDto MapTask(DashboardTaskProjection task) =>
        new(
            task.Id,
            task.Title,
            task.TaskType,
            task.PlannedDate,
            task.ActualDate,
            task.Status.ToString(),
            task.Priority.ToString());

    private sealed record ExportPlanProjection(
        int Id,
        string Name,
        ExportPlanStatus Status,
        string Crop,
        string Destination,
        string Governorate,
        string City,
        decimal AreaInFeddan,
        DateOnly PlantingDate,
        DateOnly ExpectedHarvestDate,
        DateOnly ExpectedExportDate);

    private sealed record DashboardTaskProjection(
        int Id,
        string Title,
        string TaskType,
        DateOnly PlannedDate,
        DateOnly? ActualDate,
        DomainTaskStatus Status,
        TaskPriority Priority);
}
