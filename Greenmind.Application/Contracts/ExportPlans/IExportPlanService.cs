using Greenmind.Application.Dtos.ExportPlans;

namespace Greenmind.Application.Contracts.ExportPlans;

public interface IExportPlanService
{
    Task<ExportPlanResult<ExportPlanDetailsDto>> CreateAsync(
        string ownerUserId,
        ExportPlanCreateRequest request,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ExportPlanListItemDto>> GetAllAsync(
        string ownerUserId,
        CancellationToken cancellationToken = default);

    Task<ExportPlanResult<ExportPlanDetailsDto>> GetByIdAsync(
        int exportPlanId,
        string ownerUserId,
        CancellationToken cancellationToken = default);
}
