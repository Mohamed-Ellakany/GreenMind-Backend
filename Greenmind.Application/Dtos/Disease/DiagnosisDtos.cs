using Greenmind.Domain.Consts.Enums;

namespace Greenmind.Application.Dtos.Disease;

public sealed record DiagnosisChecklistItemDto(
    string Key,
    string Label,
    bool IsComplete,
    bool IsRequired = true);

public sealed record DiagnosisReadinessDto(
    bool IsReady,
    IReadOnlyList<DiagnosisChecklistItemDto> Checklist,
    IReadOnlyList<string> MissingItems);

public sealed record DiagnosisFactsDto(
    string? AffectedPart,
    string? Onset,
    string? SpreadPattern,
    string? AffectedExtent,
    string? Conditions,
    string? PreviousTreatments);

public sealed record DiagnosisImageDto(
    int Id,
    int? MessageId,
    string OriginalFileName,
    string ContentType,
    long FileSize);

public sealed record DiagnosisMessageDto(
    int Id,
    ChatMessageRole Role,
    string Content,
    ChatMessageStatus Status,
    DateTime CreatedAt,
    IReadOnlyList<DiagnosisImageDto> Images);

public sealed record DiagnosisSourceDto(
    int Id,
    string SourceName,
    string SourceUrl,
    string? Publisher,
    string? SectionReference,
    string Excerpt,
    DateTime RetrievedAtUtc,
    bool IsOfficialOrAcademic);

public sealed record DiagnosisSummaryDto(
    int Id,
    int ExportPlanId,
    string Title,
    DiseaseAnalysisStatus Status,
    DateOnly ObservationDate,
    string? SuspectedDisease,
    decimal? ConfidencePercentage,
    DiseaseSeverity? Severity,
    int ImageCount,
    bool FinalizedWithOverride,
    bool KnowledgeVerified,
    DateTime CreatedAt,
    DateTime? ClosedAtUtc,
    DateTime? CompletedAtUtc);

public sealed record DiagnosisDetailDto(
    DiagnosisSummaryDto Summary,
    DiagnosisReadinessDto Readiness,
    DiagnosisFactsDto Facts,
    string? RecommendedActions,
    string? AdvisoryWarning,
    string? ExportImpact,
    string? FailureMessage,
    string? KnowledgeFailureMessage,
    int? TreatmentProposalId,
    IReadOnlyList<DiagnosisMessageDto> Messages,
    IReadOnlyList<DiagnosisSourceDto> Sources);

public sealed record CreateDiagnosisMessageRequest(
    string Content,
    string IdempotencyKey,
    IReadOnlyList<IncomingPrivateFile> Images);

public sealed record DiagnosisTurnDto(
    DiagnosisMessageDto UserMessage,
    DiagnosisMessageDto AssistantMessage,
    DiagnosisReadinessDto Readiness,
    DiagnosisFactsDto Facts);

public sealed record FinalizeDiagnosisRequest(bool Force);

