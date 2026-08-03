using Greenmind.Application.Contracts.Planning;
using Greenmind.Domain.Consts.Enums;
using Greenmind.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Greenmind.Api.HostedServices;

public sealed class InitialExportPlanPlanningService(
    IServiceScopeFactory scopeFactory,
    ILogger<InitialExportPlanPlanningService> logger,
    TimeProvider timeProvider) : BackgroundService
{
    private static readonly TimeSpan[] RetryDelays =
    [
        TimeSpan.FromSeconds(5),
        TimeSpan.FromSeconds(15),
        TimeSpan.FromSeconds(45)
    ];

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            await RecoverJobsAsync(stoppingToken);

            using var timer = new PeriodicTimer(TimeSpan.FromSeconds(2), timeProvider);
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await ProcessNextAsync(stoppingToken);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception exception)
                {
                    logger.LogError(exception, "Initial export-plan planning worker failed while polling.");
                }

                if (!await timer.WaitForNextTickAsync(stoppingToken))
                    break;
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Normal host shutdown, including shutdown caused by a startup failure.
        }
        catch (ObjectDisposedException) when (stoppingToken.IsCancellationRequested)
        {
            // The root provider may already be disposed after another hosted
            // service or Kestrel fails during startup.
        }
    }

    private async Task ProcessNextAsync(CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow().UtcDateTime;
        int? exportPlanId;

        await using (var claimScope = scopeFactory.CreateAsyncScope())
        {
            var db = claimScope.ServiceProvider.GetRequiredService<GreenmindDbContext>();
            var candidate = await db.ExportPlans
                .Where(plan =>
                    plan.PlanningStatus == PlanningGenerationStatus.Pending &&
                    (plan.NextPlanningAttemptAtUtc == null || plan.NextPlanningAttemptAtUtc <= now))
                .OrderBy(plan => plan.PlanningRequestedAtUtc)
                .FirstOrDefaultAsync(cancellationToken);

            if (candidate is null)
            {
                return;
            }

            candidate.PlanningStatus = PlanningGenerationStatus.InProgress;
            candidate.PlanningStartedAtUtc = now;
            candidate.PlanningAttemptCount++;
            candidate.PlanningFailureMessage = null;
            await db.SaveChangesAsync(cancellationToken);
            exportPlanId = candidate.Id;
        }

        try
        {
            await using var planningScope = scopeFactory.CreateAsyncScope();
            var planner = planningScope.ServiceProvider.GetRequiredService<IExportPlanPlanningService>();
            var count = await planner.GenerateInitialTasksAsync(exportPlanId.Value, cancellationToken);
            var db = planningScope.ServiceProvider.GetRequiredService<GreenmindDbContext>();
            var plan = await db.ExportPlans.SingleAsync(item => item.Id == exportPlanId, cancellationToken);
            plan.PlanningStatus = PlanningGenerationStatus.Succeeded;
            plan.PlanningCompletedAtUtc = timeProvider.GetUtcNow().UtcDateTime;
            plan.NextPlanningAttemptAtUtc = null;
            plan.GeneratedTaskCount = count;
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            await using var failureScope = scopeFactory.CreateAsyncScope();
            var db = failureScope.ServiceProvider.GetRequiredService<GreenmindDbContext>();
            var plan = await db.ExportPlans.SingleAsync(item => item.Id == exportPlanId, cancellationToken);
            var persistedTaskCount = await CountInitialTasksAsync(db, plan, cancellationToken);

            // A second planner may have committed the tasks before this worker observed
            // a uniqueness conflict or another post-persistence failure. In that case the
            // durable result is authoritative and the job must not be shown as failed.
            if (persistedTaskCount > 0)
            {
                plan.PlanningStatus = PlanningGenerationStatus.Succeeded;
                plan.PlanningCompletedAtUtc = timeProvider.GetUtcNow().UtcDateTime;
                plan.NextPlanningAttemptAtUtc = null;
                plan.GeneratedTaskCount = persistedTaskCount;
                plan.PlanningFailureMessage = null;
                await db.SaveChangesAsync(cancellationToken);
                logger.LogWarning(
                    exception,
                    "Initial AI planning for export plan {ExportPlanId} raised an error after {TaskCount} tasks were persisted; the job was reconciled as succeeded.",
                    exportPlanId,
                    persistedTaskCount);
                return;
            }

            var exhausted = plan.PlanningAttemptCount >= RetryDelays.Length;
            plan.PlanningStatus = exhausted
                ? PlanningGenerationStatus.Failed
                : PlanningGenerationStatus.Pending;
            plan.NextPlanningAttemptAtUtc = exhausted
                ? null
                : timeProvider.GetUtcNow().UtcDateTime.Add(RetryDelays[plan.PlanningAttemptCount - 1]);
            plan.PlanningCompletedAtUtc = exhausted ? timeProvider.GetUtcNow().UtcDateTime : null;
            plan.PlanningFailureMessage = exhausted
                ? "AI planning could not be completed. Check the configured AI and weather services, then retry."
                : null;
            await db.SaveChangesAsync(cancellationToken);
            logger.LogError(exception, "Initial AI planning failed for export plan {ExportPlanId} on attempt {Attempt}.", exportPlanId, plan.PlanningAttemptCount);
        }
    }

    private async Task RecoverJobsAsync(CancellationToken cancellationToken)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<GreenmindDbContext>();
        var cutoff = timeProvider.GetUtcNow().UtcDateTime.AddMinutes(-10);
        var unfinished = await db.ExportPlans
            .Where(plan =>
                plan.PlanningStatus == PlanningGenerationStatus.Failed ||
                plan.GeneratedTaskCount > 0 ||
                (plan.PlanningStatus == PlanningGenerationStatus.InProgress &&
                 plan.PlanningStartedAtUtc < cutoff))
            .ToListAsync(cancellationToken);

        var changed = false;
        foreach (var plan in unfinished)
        {
            var persistedTaskCount = await CountInitialTasksAsync(db, plan, cancellationToken);
            if (persistedTaskCount > 0)
            {
                plan.PlanningStatus = PlanningGenerationStatus.Succeeded;
                plan.PlanningCompletedAtUtc ??= timeProvider.GetUtcNow().UtcDateTime;
                plan.NextPlanningAttemptAtUtc = null;
                plan.GeneratedTaskCount = persistedTaskCount;
                plan.PlanningFailureMessage = null;
                changed = true;
                continue;
            }

            if (plan.PlanningStatus == PlanningGenerationStatus.InProgress &&
                plan.PlanningStartedAtUtc < cutoff)
            {
                plan.PlanningStatus = PlanningGenerationStatus.Pending;
                plan.NextPlanningAttemptAtUtc = timeProvider.GetUtcNow().UtcDateTime;
                changed = true;
            }
        }

        if (changed)
        {
            await db.SaveChangesAsync(cancellationToken);
        }
    }

    private DateOnly InitialPlanningStartDate() =>
        DateOnly.FromDateTime(timeProvider.GetUtcNow().UtcDateTime).AddDays(1);

    private async Task<int> CountInitialTasksAsync(
        GreenmindDbContext db,
        Greenmind.Domain.Entities.ExportPlan plan,
        CancellationToken cancellationToken)
    {
        var start = InitialPlanningStartDate();
        var sevenDayHorizon = start.AddDays(6);
        var end = plan.ExpectedExportDate < sevenDayHorizon
            ? plan.ExpectedExportDate
            : sevenDayHorizon;

        if (end < start)
        {
            return 0;
        }

        return await db.FarmTasks
            .AsNoTracking()
            .CountAsync(task =>
                task.ExportPlanId == plan.Id &&
                task.PlannedDate >= start &&
                task.PlannedDate <= end,
                cancellationToken);
    }
}
