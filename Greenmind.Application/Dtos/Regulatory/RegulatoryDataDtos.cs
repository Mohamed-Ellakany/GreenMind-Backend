namespace Greenmind.Application.Dtos.Regulatory;

public sealed record RegulatoryImportResultDto(
    string Message,
    int ImportedRows,
    int SkippedRows);

public sealed record ActiveSubstanceKnowledgeDto(
    int ExternalId,
    string RegionName,
    string Name,
    string? CasNumber,
    string ApprovalStatus,
    DateOnly? ApprovalExpiresOn,
    string? Legislation,
    string? Remark,
    string? RegulationReference,
    string? Functions);

public sealed record ProductMrlKnowledgeDto(
    string RegionName,
    string ProductType,
    string ExternalPesticideId,
    string ResidueName,
    decimal? MaximumResidueLevel,
    bool IsDetectionLimit,
    string Unit);
