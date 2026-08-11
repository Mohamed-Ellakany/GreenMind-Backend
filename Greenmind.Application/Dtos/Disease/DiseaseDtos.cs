using Greenmind.Domain.Consts.Enums;

namespace Greenmind.Application.Dtos.Disease;

public sealed record IncomingPrivateFile(
    string FileName,
    string ClaimedContentType,
    byte[] Content);

public sealed record CreateDiseaseReportRequest(
    DateOnly ObservationDate,
    string? AffectedArea,
    string? Notes,
    IReadOnlyList<IncomingPrivateFile> Images);

public sealed record DiseaseImageDto(
    int Id,
    string OriginalFileName,
    string ContentType,
    long FileSize);

public sealed record DiseaseImageDownload(
    byte[] Content,
    string ContentType,
    string FileName);

public sealed record DiseaseReportDto(
    int Id,
    int ExportPlanId,
    DateOnly ObservationDate,
    string? AffectedArea,
    string? UserNotes,
    DiseaseAnalysisStatus Status,
    string? SuspectedDisease,
    decimal? ConfidencePercentage,
    DiseaseSeverity? Severity,
    string? ObservedSymptoms,
    string? RecommendedActions,
    string? ExportImpact,
    string? AdvisoryWarning,
    string? FailureMessage,
    DateTime? CompletedAtUtc,
    DateTime CreatedAt,
    IReadOnlyList<DiseaseImageDto> Images);

public sealed record DiseaseReportPageDto(
    IReadOnlyList<DiseaseReportDto> Items,
    int Page,
    int PageSize,
    int TotalCount);

public sealed record DiseaseVisionImage(
    string ContentType,
    byte[] Content);

public sealed record DiseaseVisionRequest(
    string PlanContext,
    DateOnly ObservationDate,
    string? AffectedArea,
    string? Notes,
    IReadOnlyList<DiseaseVisionImage> Images,
    string? KnowledgeContext = null,
    int? ExportPlanId = null);

public sealed record DiseaseVisionResult(
    string SuspectedDisease,
    decimal ConfidencePercentage,
    DiseaseSeverity Severity,
    string ObservedSymptoms,
    string RecommendedActions,
    string ExportImpact,
    string AdvisoryWarning,
    string ModelName,
    string PromptVersion);
