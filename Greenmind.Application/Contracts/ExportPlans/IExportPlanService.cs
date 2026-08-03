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

    Task<ExportPlanResult<ExportPlanScheduleAnalysisDto>> AnalyzeScheduleAsync(
        string ownerUserId,
        ExportPlanScheduleAnalysisRequest request,
        CancellationToken cancellationToken = default);

    Task<ExportPlanResult<PlanningStatusDto>> GetPlanningStatusAsync(
        int exportPlanId,
        string ownerUserId,
        CancellationToken cancellationToken = default);

    Task<ExportPlanResult<PlanningStatusDto>> RetryPlanningAsync(
        int exportPlanId,
        string ownerUserId,
        CancellationToken cancellationToken = default);
}

public interface IExportScheduleAdvisor
{
    Task<ExportScheduleAdvisorResult> AnalyzeAsync(
        ExportScheduleAdvisorContext context,
        CancellationToken cancellationToken = default);
}
