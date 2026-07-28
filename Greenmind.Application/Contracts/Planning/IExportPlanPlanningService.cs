using Greenmind.Application.Dtos.ExportPlans;

namespace Greenmind.Application.Contracts.Planning;

public interface IExportPlanPlanningService
{
    Task<int> GenerateInitialTasksAsync(int exportPlanId, CancellationToken cancellationToken = default);
}
