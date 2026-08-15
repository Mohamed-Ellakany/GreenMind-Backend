using Greenmind.Application.Contracts.Dashboard;
using Greenmind.Application.Contracts.Planning;
using Greenmind.Application.Dtos.AI;
using Greenmind.Application.Interfaces.AI;
using Greenmind.Domain.Consts;
using Greenmind.Domain.Consts.Enums;
using Greenmind.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Greenmind.Infrastructure.Planning;

public sealed class ExportPlanPlanningService(
    GreenmindDbContext dbContext,
    IAgentPlanningService agentPlanningService,
    IFarmTaskPersistenceService taskPersistenceService,
    IWeatherHttpClient weatherHttpClient,
    ISoilMeasurementRepository soilRepository,
    ISeasonCalculationService seasonCalculationService,
    TimeProvider timeProvider) : IExportPlanPlanningService
{
    public async Task<IReadOnlyList<TaskItemDto>> GenerateRevisionTasksAsync(
        int exportPlanId,
        CancellationToken cancellationToken = default)
    {
        var plan = await dbContext.ExportPlans
            .AsNoTracking()
            .Include(item => item.Farm)
            .Include(item => item.Crop)
            .Include(item => item.Destination)
            .SingleAsync(item => item.Id == exportPlanId, cancellationToken);

        var today = DateOnly.FromDateTime(timeProvider.GetUtcNow().UtcDateTime);
        var start = today;
        var end = plan.ExpectedExportDate < today.AddDays(7)
            ? plan.ExpectedExportDate
            : today.AddDays(7);
        if (end < start)
            return [];

        var season = seasonCalculationService.Calculate(plan.PlantingDate, plan.ExpectedHarvestDate, today);
        var weatherTask = weatherHttpClient.GetForecastAsync(plan.Latitude, plan.Longitude, start, end);
        var soilHistory = await soilRepository.GetHistoryAsync(plan.Id, today.AddDays(-90));
        var latestSoil = await soilRepository.GetLatestAsync(plan.Id);
        var taskTypes = await dbContext.TaskTypes.AsNoTracking().OrderBy(item => item.Id)
            .Select(item => new AvailableTaskTypeDto(item.Name, item.Description)).ToListAsync(cancellationToken);
        var schedule = await dbContext.FarmTasks.AsNoTracking()
            .Where(task => task.ExportPlan.FarmId == plan.FarmId && task.PlannedDate >= start && task.PlannedDate <= end)
            .OrderBy(task => task.PlannedDate)
            .Select(task => new HistoricalTaskDto
            {
                ExportPlanId = task.ExportPlanId, PlanName = task.ExportPlan.Name,
                TaskTypeName = task.TaskType.Name, Title = task.Title, Description = task.Description,
                PlannedDate = task.PlannedDate, ActualDate = task.ActualDate,
                Status = task.Status.ToString(), Priority = task.Priority, Notes = task.Notes
                , Origin = task.Origin.ToString()
            }).ToListAsync(cancellationToken);
        var request = new AgentRequest
        {
            Mode = "revision",
            ExportPlan = new ExportPlanDto
            {
                ExportPlanId = plan.Id, PlanName = plan.Name, FarmName = plan.Farm.Name,
                CropId = plan.CropId, Crop = plan.Crop.Name, CropDescription = plan.Crop.Description,
                Destination = plan.Destination.Name, DestinationDescription = plan.Destination.Description,
                Governorate = plan.Governorate, City = plan.City, Latitude = plan.Latitude, Longitude = plan.Longitude,
                AreaInFeddan = plan.AreaInFeddan, PlantingDate = plan.PlantingDate,
                ExpectedHarvestDate = plan.ExpectedHarvestDate, ExpectedExportDate = plan.ExpectedExportDate,
                CurrentSeasonDay = season.CurrentSeasonDay, GrowthStage = season.GrowthStage, Notes = plan.Notes,
                FarmType = plan.FarmType, SoilType = plan.SoilType, IrrigationSystem = plan.IrrigationSystem, Fertigation = plan.Fertigation,
                SoilPh = plan.SoilPh, SoilEc = plan.SoilEc, SoilNitrogen = plan.SoilNitrogen, SoilPhosphorus = plan.SoilPhosphorus, SoilPotassium = plan.SoilPotassium, SoilOrganicMatter = plan.SoilOrganicMatter,
                WaterPh = plan.WaterPh, WaterEc = plan.WaterEc, WaterSalinity = plan.WaterSalinity, WaterSodium = plan.WaterSodium, WaterChloride = plan.WaterChloride, WaterBicarbonate = plan.WaterBicarbonate, WaterCalcium = plan.WaterCalcium, WaterMagnesium = plan.WaterMagnesium, WaterSar = plan.WaterSar
            },
            PlanningWindow = new PlanningWindowDto { StartDate = start, EndDate = end, DaysToGenerate = end.DayNumber - start.DayNumber + 1 },
            LatestSoilMeasurement = latestSoil,
            SoilHistory = soilHistory,
            WeatherForecast = await weatherTask,
            ExistingFarmSchedule = schedule,
            AvailableTaskTypes = taskTypes
        };
        var response = await agentPlanningService.GeneratePlanAsync(request);
        var knownTaskTypes = taskTypes.Select(item => item.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var cropPhi = EuComplianceRules.GetPreHarvestIntervalDays(plan.CropId, fallbackDays: 7);
        return response.Tasks
            .Where(task => task.PlannedDate >= start && task.PlannedDate <= end && task.Quantity > 0 &&
                           !string.IsNullOrWhiteSpace(task.Unit) && knownTaskTypes.Contains(task.TaskTypeName) &&
                           (!task.TaskTypeName.Equals("PestControl", StringComparison.OrdinalIgnoreCase) ||
                            task.PlannedDate <= plan.ExpectedHarvestDate.AddDays(-cropPhi)))
            .GroupBy(task => new { Type = task.TaskTypeName.ToUpperInvariant(), task.PlannedDate })
            .Select(group => group.First())
            .ToList();
    }

    public async Task<int> GenerateInitialTasksAsync(
        int exportPlanId,
        CancellationToken cancellationToken = default)
    {
        var plan = await dbContext.ExportPlans
            .AsNoTracking()
            .Include(item => item.Farm)
            .Include(item => item.Crop)
            .Include(item => item.Destination)
            .SingleAsync(item => item.Id == exportPlanId, cancellationToken);

        var today = DateOnly.FromDateTime(timeProvider.GetUtcNow().UtcDateTime);
        var start = today.AddDays(1);
        var end = plan.ExpectedExportDate < today.AddDays(7)
            ? plan.ExpectedExportDate
            : today.AddDays(7);

        if (end < start)
        {
            throw new InvalidOperationException("The export plan has no future dates available for planning.");
        }

        var historyStart = today.AddDays(-90);
        var season = seasonCalculationService.Calculate(plan.PlantingDate, plan.ExpectedHarvestDate, today);

        // The weather request does not use EF, so it can run while database context is loaded.
        // EF Core DbContext instances themselves must only execute one operation at a time.
        var weatherTask = weatherHttpClient.GetForecastAsync(
            plan.Latitude,
            plan.Longitude,
            start,
            end);
        var soilHistory = await soilRepository.GetHistoryAsync(plan.Id, historyStart);
        var latestSoil = await soilRepository.GetLatestAsync(plan.Id);
        var taskTypes = await dbContext.TaskTypes
            .AsNoTracking()
            .OrderBy(item => item.Id)
            .Select(item => new AvailableTaskTypeDto(item.Name, item.Description))
            .ToListAsync(cancellationToken);
        var historicalTasks = await dbContext.FarmTasks
            .AsNoTracking()
            .Where(task =>
                task.ExportPlan.FarmId == plan.FarmId &&
                task.ExportPlan.CropId == plan.CropId &&
                task.ExportPlanId != plan.Id &&
                task.PlannedDate >= historyStart &&
                task.PlannedDate <= today)
            .OrderByDescending(task => task.PlannedDate)
            .Select(task => new HistoricalTaskDto
            {
                ExportPlanId = task.ExportPlanId,
                PlanName = task.ExportPlan.Name,
                TaskTypeName = task.TaskType.Name,
                Title = task.Title,
                Description = task.Description,
                PlannedDate = task.PlannedDate,
                ActualDate = task.ActualDate,
                Status = task.Status.ToString(),
                Priority = task.Priority,
                Notes = task.Notes
            })
            .ToListAsync(cancellationToken);
        var farmSchedule = await dbContext.FarmTasks
            .AsNoTracking()
            .Where(task =>
                task.ExportPlan.FarmId == plan.FarmId &&
                task.PlannedDate >= start &&
                task.PlannedDate <= end)
            .OrderBy(task => task.PlannedDate)
            .Select(task => new HistoricalTaskDto
            {
                ExportPlanId = task.ExportPlanId,
                PlanName = task.ExportPlan.Name,
                TaskTypeName = task.TaskType.Name,
                Title = task.Title,
                Description = task.Description,
                PlannedDate = task.PlannedDate,
                ActualDate = task.ActualDate,
                Status = task.Status.ToString(),
                Priority = task.Priority,
                Notes = task.Notes
            })
            .ToListAsync(cancellationToken);
        var weather = await weatherTask;

        var request = new AgentRequest
        {
            Mode = "initial",
            ExportPlan = new ExportPlanDto
            {
                ExportPlanId = plan.Id,
                PlanName = plan.Name,
                FarmName = plan.Farm.Name,
                CropId = plan.CropId,
                Crop = plan.Crop.Name,
                CropDescription = plan.Crop.Description,
                Destination = plan.Destination.Name,
                DestinationDescription = plan.Destination.Description,
                Governorate = plan.Governorate,
                City = plan.City,
                Latitude = plan.Latitude,
                Longitude = plan.Longitude,
                AreaInFeddan = plan.AreaInFeddan,
                PlantingDate = plan.PlantingDate,
                ExpectedHarvestDate = plan.ExpectedHarvestDate,
                ExpectedExportDate = plan.ExpectedExportDate,
                CurrentSeasonDay = season.CurrentSeasonDay,
                GrowthStage = season.GrowthStage,
                Notes = plan.Notes,
                FarmType = plan.FarmType,
                SoilType = plan.SoilType,
                IrrigationSystem = plan.IrrigationSystem,
                Fertigation = plan.Fertigation,
                SoilPh = plan.SoilPh,
                SoilEc = plan.SoilEc,
                SoilNitrogen = plan.SoilNitrogen,
                SoilPhosphorus = plan.SoilPhosphorus,
                SoilPotassium = plan.SoilPotassium,
                SoilOrganicMatter = plan.SoilOrganicMatter,
                WaterPh = plan.WaterPh,
                WaterEc = plan.WaterEc,
                WaterSalinity = plan.WaterSalinity,
                WaterSodium = plan.WaterSodium,
                WaterChloride = plan.WaterChloride,
                WaterBicarbonate = plan.WaterBicarbonate,
                WaterCalcium = plan.WaterCalcium,
                WaterMagnesium = plan.WaterMagnesium,
                WaterSar = plan.WaterSar
            },
            PlanningWindow = new PlanningWindowDto
            {
                StartDate = start,
                EndDate = end,
                DaysToGenerate = end.DayNumber - start.DayNumber + 1
            },
            LatestSoilMeasurement = latestSoil,
            SoilHistory = soilHistory,
            WeatherForecast = weather,
            HistoricalTasks = historicalTasks,
            ExistingFarmSchedule = farmSchedule,
            AvailableTaskTypes = taskTypes
        };

        var response = await agentPlanningService.GeneratePlanAsync(request);
        var knownTaskTypes = request.AvailableTaskTypes
            .Select(item => item.Name)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var cropPhi = EuComplianceRules.GetPreHarvestIntervalDays(plan.CropId, fallbackDays: 7);

        response.Tasks = response.Tasks
            .Where(task =>
                task.PlannedDate >= start &&
                task.PlannedDate <= end &&
                task.Quantity > 0 &&
                !string.IsNullOrWhiteSpace(task.Unit) &&
                knownTaskTypes.Contains(task.TaskTypeName) &&
                (!task.TaskTypeName.Equals("PestControl", StringComparison.OrdinalIgnoreCase) ||
                 task.PlannedDate <= plan.ExpectedHarvestDate.AddDays(-cropPhi)))
            .GroupBy(task => new { Type = task.TaskTypeName.ToUpperInvariant(), task.PlannedDate })
            .Select(group => group.First())
            .ToList();

        if (response.Tasks.Count == 0)
        {
            throw new InvalidOperationException("The AI planner did not return any valid tasks for the planning window.");
        }

        await taskPersistenceService.SaveGeneratedTasksAsync(plan.Id, response);

        return await dbContext.FarmTasks
            .AsNoTracking()
            .CountAsync(task =>
                task.ExportPlanId == plan.Id &&
                task.PlannedDate >= start &&
                task.PlannedDate <= end,
                cancellationToken);
    }
}
