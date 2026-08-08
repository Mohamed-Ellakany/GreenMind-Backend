using Greenmind.Application.Dtos.ExportPlans;
using Greenmind.Application.Dtos.AI;

namespace Greenmind.Application.Contracts.Planning;

public interface IExportPlanPlanningService
{
    Task<int> GenerateInitialTasksAsync(int exportPlanId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<TaskItemDto>> GenerateRevisionTasksAsync(int exportPlanId, CancellationToken cancellationToken = default);
}
