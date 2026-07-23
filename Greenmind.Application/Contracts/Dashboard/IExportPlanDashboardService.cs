using Greenmind.Application.Dtos.Dashboard;

namespace Greenmind.Application.Contracts.Dashboard;

public interface IExportPlanDashboardService
{
    Task<ExportPlanDashboardDto?> GetAsync(
        int exportPlanId,
        string ownerUserId,
        CancellationToken cancellationToken = default);
}
