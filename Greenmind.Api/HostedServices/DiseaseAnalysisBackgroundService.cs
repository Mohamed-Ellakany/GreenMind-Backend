using Greenmind.Application.Contracts.Disease;

namespace Greenmind.Api.HostedServices;

public sealed class DiseaseAnalysisBackgroundService(
    IServiceScopeFactory scopeFactory,
    IConfiguration configuration,
    ILogger<DiseaseAnalysisBackgroundService> logger) : BackgroundService
{
    private int _consecutiveFailures;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var baseDelay = TimeSpan.FromSeconds(Math.Clamp(
            configuration.GetValue("DiseaseAnalysis:WorkerDelaySeconds", 5),
            1,
            60));

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = scopeFactory.CreateScope();
                var service = scope.ServiceProvider
                    .GetRequiredService<IDiseaseReportService>();
                if (await service.ProcessNextPendingAsync(stoppingToken))
                {
                    _consecutiveFailures = 0;
                    continue;
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (OutOfMemoryException exception)
            {
                _consecutiveFailures++;
                logger.LogCritical(
                    exception,
                    "Disease analysis worker ran out of memory (failure #{Count}). " +
                    "Forcing GC collection and backing off.",
                    _consecutiveFailures);

                // Attempt to reclaim memory before the next iteration crashes the process.
                GC.Collect(2, GCCollectionMode.Aggressive, blocking: true);
                GC.WaitForPendingFinalizers();

                await BackoffAsync(baseDelay, stoppingToken);
                continue;
            }
            catch (Exception exception)
            {
                _consecutiveFailures++;
                logger.LogError(
                    exception,
                    "Disease analysis worker iteration failed (failure #{Count}).",
                    _consecutiveFailures);

                await BackoffAsync(baseDelay, stoppingToken);
                continue;
            }

            _consecutiveFailures = 0;
            await Task.Delay(baseDelay, stoppingToken);
        }
    }

    /// <summary>
    /// Exponential backoff capped at 5 minutes to prevent a tight crash loop
    /// that repeatedly kills the IIS application pool.
    /// </summary>
    private async Task BackoffAsync(TimeSpan baseDelay, CancellationToken stoppingToken)
    {
        var backoff = TimeSpan.FromSeconds(
            Math.Min(baseDelay.TotalSeconds * Math.Pow(2, _consecutiveFailures), 300));
        logger.LogWarning(
            "Backing off disease worker for {Seconds:F0}s after {Count} consecutive failure(s).",
            backoff.TotalSeconds,
            _consecutiveFailures);
        await Task.Delay(backoff, stoppingToken);
    }
}
