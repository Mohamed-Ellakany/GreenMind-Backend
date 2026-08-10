using Greenmind.Application.Contracts.Planning;
using Greenmind.Domain.Consts.Enums;
using Greenmind.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Greenmind.Api.HostedServices;

public sealed class ScheduleRevisionWorker(
    IServiceScopeFactory scopeFactory,
    ILogger<ScheduleRevisionWorker> logger,
    TimeProvider timeProvider) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            await RecoverStaleJobsAsync(stoppingToken);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            return;
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Schedule revision worker failed while recovering interrupted jobs.");
        }

        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(3), timeProvider);
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                int? revisionId;
                await using (var scope = scopeFactory.CreateAsyncScope())
                {
                    var db = scope.ServiceProvider.GetRequiredService<GreenmindDbContext>();
                    var revision = await db.ScheduleRevisions
                        .Where(item => item.Status == ScheduleRevisionStatus.Pending)
                        .OrderBy(item => item.CreatedAt)
                        .FirstOrDefaultAsync(stoppingToken);
                    revisionId = revision?.Id;
                }
                if (revisionId.HasValue)
                {
                    await using var scope = scopeFactory.CreateAsyncScope();
                    await scope.ServiceProvider.GetRequiredService<IScheduleRevisionService>()
                        .ProcessAsync(revisionId.Value, stoppingToken);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "Schedule revision worker failed while polling.");
            }

            if (!await timer.WaitForNextTickAsync(stoppingToken)) break;
        }
    }

    private async Task RecoverStaleJobsAsync(CancellationToken cancellationToken)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<GreenmindDbContext>();
        var cutoff = timeProvider.GetUtcNow().UtcDateTime.AddMinutes(-10);
        var stale = await db.ScheduleRevisions
            .Where(item => item.Status == ScheduleRevisionStatus.InProgress && item.StartedAtUtc < cutoff)
            .ToListAsync(cancellationToken);
        if (stale.Count == 0) return;
        foreach (var revision in stale)
        {
            revision.Status = ScheduleRevisionStatus.Pending;
            revision.StartedAtUtc = null;
        }
        await db.SaveChangesAsync(cancellationToken);
        logger.LogWarning("Re-queued {Count} interrupted schedule revisions.", stale.Count);
    }
}
