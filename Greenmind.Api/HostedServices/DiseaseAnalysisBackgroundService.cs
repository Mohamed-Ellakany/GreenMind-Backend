using Greenmind.Application.Contracts.Disease;

namespace Greenmind.Api.HostedServices;

public sealed class DiseaseAnalysisBackgroundService(
    IServiceScopeFactory scopeFactory,
    IConfiguration configuration,
    ILogger<DiseaseAnalysisBackgroundService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var delay = TimeSpan.FromSeconds(Math.Clamp(
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
                    continue;
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "Disease analysis worker iteration failed.");
            }

            await Task.Delay(delay, stoppingToken);
        }
    }
}
