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
        await RecoverStaleJobsAsync(stoppingToken);

        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(2), timeProvider);
        do
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
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
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
        catch (Exception exception)
        {
            await using var failureScope = scopeFactory.CreateAsyncScope();
            var db = failureScope.ServiceProvider.GetRequiredService<GreenmindDbContext>();
            var plan = await db.ExportPlans.SingleAsync(item => item.Id == exportPlanId, cancellationToken);
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

    private async Task RecoverStaleJobsAsync(CancellationToken cancellationToken)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<GreenmindDbContext>();
        var cutoff = timeProvider.GetUtcNow().UtcDateTime.AddMinutes(-10);
        var stale = await db.ExportPlans
            .Where(plan =>
                plan.PlanningStatus == PlanningGenerationStatus.InProgress &&
                plan.PlanningStartedAtUtc < cutoff)
            .ToListAsync(cancellationToken);

        foreach (var plan in stale)
        {
            plan.PlanningStatus = PlanningGenerationStatus.Pending;
            plan.NextPlanningAttemptAtUtc = timeProvider.GetUtcNow().UtcDateTime;
        }

        if (stale.Count > 0)
        {
            await db.SaveChangesAsync(cancellationToken);
        }
    }
}
