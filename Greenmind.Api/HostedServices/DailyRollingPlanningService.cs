using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Greenmind.Application.Contracts.Persistence;
using Greenmind.Application.Interfaces.AI;
using Greenmind.Application.Services;
using Greenmind.Application.Dtos.AI;
using Greenmind.Domain.Consts.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Greenmind.Api.HostedServices;

public class DailyRollingPlanningService : BackgroundService
{
    private readonly ILogger<DailyRollingPlanningService> _logger;
    private readonly IServiceProvider _services;

    public DailyRollingPlanningService(
        ILogger<DailyRollingPlanningService> logger,
        IServiceProvider services)
    {
        _logger = logger;
        _services = services;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("DailyRollingPlanningService started.");

        // Run once at startup then every 24 hours
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await RunOnceAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (ObjectDisposedException) when (stoppingToken.IsCancellationRequested)
            {
                // The host can dispose its service provider when startup fails
                // before this worker observes cancellation.
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unhandled error in DailyRollingPlanningService.");
            }

            try
            {
                await Task.Delay(TimeSpan.FromHours(24), stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                // shutting down
                break;
            }
        }
    }

    public async Task RunOnceAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Daily rolling planning run started.");

        // Resolve scoped services inside a scope to avoid consuming scoped services from a singleton
        using var scope = _services.CreateScope();
        var uow = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
        var agentService = scope.ServiceProvider.GetRequiredService<IAgentPlanningService>();
        var persistence = scope.ServiceProvider.GetRequiredService<IFarmTaskPersistenceService>();
        var calcService = scope.ServiceProvider.GetRequiredService<IFuturePlannedDaysCalculationService>();
        var soilRepo = scope.ServiceProvider.GetRequiredService<Greenmind.Application.Interfaces.AI.ISoilMeasurementRepository>();
        var taskHistoryRepo = scope.ServiceProvider.GetRequiredService<Greenmind.Application.Interfaces.AI.ITaskHistoryRepository>();

        // Select active export plans (Planning or InProgress). Use AsNoTracking and process in pages to avoid loading all plans into memory.
        var baseQuery = uow.ExportPlans.Query()
            .Where(p =>
                (p.Status == ExportPlanStatus.Planning || p.Status == ExportPlanStatus.InProgress) &&
                p.PlanningStatus == PlanningGenerationStatus.Succeeded)
            .Include(p => p.Crop)
            .Include(p => p.Destination)
            .AsNoTracking();

        const int pageSize = 200;
        var page = 0;
        while (!cancellationToken.IsCancellationRequested)
        {
            var plans = await baseQuery.Skip(page * pageSize).Take(pageSize).ToListAsync(cancellationToken);
            if (plans == null || plans.Count == 0) break;

            foreach (var plan in plans)
            {
                try
                {
                    if (cancellationToken.IsCancellationRequested) break;
                    var today = DateOnly.FromDateTime(DateTime.UtcNow);
                // Skip plans outside season window
                if (today < plan.PlantingDate || today > plan.ExpectedExportDate)
                {
                    _logger.LogInformation("Skipping plan {PlanId} because today {Today} is outside season {Start}..{End}", plan.Id, today, plan.PlantingDate, plan.ExpectedExportDate);
                    continue;
                }

                var futureCount = await calcService.CountFuturePlannedDaysAsync(plan, 7, cancellationToken);
                if (futureCount >= 6)
                {
                    _logger.LogInformation("Skipping plan {PlanId}: already has {Count} future planned days.", plan.Id, futureCount);
                    continue;
                }

                var missingDates = await calcService.GetMissingFuturePlannedDatesAsync(plan, cancellationToken);
                if (missingDates.Count == 0)
                {
                    _logger.LogInformation("No missing dates for plan {PlanId}.", plan.Id);
                    continue;
                }

                // If exactly 5 future days -> continuous planning (we still generate missing days to reach 7)
                if (futureCount == 5)
                {
                    _logger.LogInformation("Plan {PlanId} has 5 future days; triggering continuous planning to fill missing days.", plan.Id);
                }

                var start = missingDates.Min();
                var end = missingDates.Max();

                var request = new AgentRequest
                {
                    Mode = futureCount == 5 ? "continuous" : "daily",
                    ExportPlan = new ExportPlanDto
                    {
                        ExportPlanId = plan.Id,
                        Crop = plan.Crop?.Name ?? string.Empty,
                        Destination = plan.Destination?.Name ?? string.Empty,
                        Governorate = plan.Governorate,
                        City = plan.City,
                        AreaInFeddan = plan.AreaInFeddan,
                        PlantingDate = plan.PlantingDate,
                        ExpectedHarvestDate = plan.ExpectedHarvestDate,
                        ExpectedExportDate = plan.ExpectedExportDate
                    },
                    PlanningWindow = new PlanningWindowDto
                    {
                        StartDate = start,
                        EndDate = end,
                        DaysToGenerate = missingDates.Count
                    },
                    TaskContext = new Greenmind.Application.Dtos.AI.TaskContextDto
                    {
                        ExistingTasks = await taskHistoryRepo.GetTasksInRangeAsync(plan.Id, today.AddDays(-14), today),
                        CompletedTasks = await taskHistoryRepo.GetCompletedTasksAsync(plan.Id, today.AddDays(-14), today),
                        CurrentTaskStatuses = new List<string>()
                    },
                    LatestSoilMeasurement = await soilRepo.GetLatestAsync(plan.Id)
                };

                var response = await agentService.GeneratePlanAsync(request);
                    if (response.Tasks != null && response.Tasks.Count > 0)
                    {
                        await persistence.SaveGeneratedTasksAsync(plan.Id, response);
                        _logger.LogInformation("Generated {Count} tasks for plan {PlanId}.", response.Tasks.Count, plan.Id);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error processing export plan {PlanId}", plan.Id);
                    // continue processing other plans
                }
            }

            page++;
        }

        _logger.LogInformation("Daily rolling planning run completed.");
    }
}
