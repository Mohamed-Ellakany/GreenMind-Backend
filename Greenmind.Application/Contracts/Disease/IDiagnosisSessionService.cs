using Greenmind.Application.Dtos.Disease;
using Greenmind.Application.Dtos.TaskProposals;

namespace Greenmind.Application.Contracts.Disease;

public interface IDiagnosisSessionService
{
    Task<DiseaseOperationResult<DiagnosisDetailDto>> CreateAsync(
        int exportPlanId, string ownerUserId, CancellationToken cancellationToken = default);

    Task<DiseaseOperationResult<IReadOnlyList<DiagnosisSummaryDto>>> ListAsync(
        int exportPlanId, string ownerUserId, CancellationToken cancellationToken = default);

    Task<DiseaseOperationResult<DiagnosisDetailDto>> GetAsync(
        int exportPlanId, int diagnosisId, string ownerUserId, CancellationToken cancellationToken = default);

    Task<DiseaseOperationResult<DiagnosisTurnDto>> SendMessageAsync(
        int exportPlanId, int diagnosisId, string ownerUserId,
        CreateDiagnosisMessageRequest request, CancellationToken cancellationToken = default);

    Task<DiseaseOperationResult<DiagnosisDetailDto>> FinalizeAsync(
        int exportPlanId, int diagnosisId, string ownerUserId, bool force,
        CancellationToken cancellationToken = default);

    Task<DiseaseOperationResult<DiagnosisDetailDto>> CancelAsync(
        int exportPlanId, int diagnosisId, string ownerUserId,
        CancellationToken cancellationToken = default);

    Task<DiseaseOperationResult<DiagnosisDetailDto>> RetryAsync(
        int exportPlanId, int diagnosisId, string ownerUserId,
        CancellationToken cancellationToken = default);

    Task<DiseaseOperationResult<DiseaseImageDownload>> DownloadImageAsync(
        int exportPlanId, int diagnosisId, int imageId, string ownerUserId,
        CancellationToken cancellationToken = default);

    Task<DiseaseOperationResult<TaskChangeProposalDto>> CreateTreatmentProposalAsync(
        int exportPlanId, int diagnosisId, string ownerUserId,
        CancellationToken cancellationToken = default);
}

