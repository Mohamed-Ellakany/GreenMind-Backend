using System.ComponentModel.DataAnnotations;
using Greenmind.Domain.Consts.Enums;

namespace Greenmind.Application.Dtos.ExportPlans;

public sealed class ExportPlanCreateRequest : IValidatableObject
{
    [Required]
    [MaxLength(200)]
    public string? Name { get; init; }

    [Required]
    public int? CropId { get; init; }

    [Required]
    public int? DestinationId { get; init; }

    [Required]
    [MaxLength(100)]
    public string? Governorate { get; init; }

    [Required]
    [MaxLength(100)]
    public string? City { get; init; }

    [Required]
    [Range(-90d, 90d)]
    public double? Latitude { get; init; }

    [Required]
    [Range(-180d, 180d)]
    public double? Longitude { get; init; }

    [Required]
    [Range(typeof(decimal), "0.01", "79228162514264337593543950335")]
    public decimal? AreaInFeddan { get; init; }

    [Required]
    public DateOnly? PlantingDate { get; init; }

    [Required]
    public DateOnly? ExpectedHarvestDate { get; init; }

    [Required]
    public DateOnly? ExpectedExportDate { get; init; }

    public string? AnalysisToken { get; init; }

    public bool AcceptScheduleWarnings { get; init; }

    // Retained for backward compatibility. New plans always start in Planning.
    public ExportPlanStatus? Status { get; init; }

    [MaxLength(1000)]
    public string? Notes { get; init; }

    [MaxLength(50)]
    public string? FarmType { get; init; }

    [MaxLength(50)]
    public string? SoilType { get; init; }

    [Range(0d, 14d)]
    public double? SoilPh { get; init; }

    [Range(0d, 100d)]
    public double? SoilEc { get; init; }

    [Range(0d, 100000d)]
    public double? SoilNitrogen { get; init; }

    [Range(0d, 100000d)]
    public double? SoilPhosphorus { get; init; }

    [Range(0d, 100000d)]
    public double? SoilPotassium { get; init; }

    [Range(0d, 100d)]
    public double? SoilOrganicMatter { get; init; }

    [Range(0d, 14d)]
    public double? WaterPh { get; init; }

    [Range(0d, 100d)]
    public double? WaterEc { get; init; }

    [Range(0d, 100000d)]
    public double? WaterSalinity { get; init; }

    [Range(0d, 100000d)]
    public double? WaterSodium { get; init; }

    [Range(0d, 100000d)]
    public double? WaterChloride { get; init; }

    [Range(0d, 100000d)]
    public double? WaterBicarbonate { get; init; }

    [Range(0d, 100000d)]
    public double? WaterCalcium { get; init; }

    [Range(0d, 100000d)]
    public double? WaterMagnesium { get; init; }

    [Range(0d, 1000d)]
    public double? WaterSar { get; init; }

    [MaxLength(50)]
    public string? IrrigationSystem { get; init; }

    [MaxLength(50)]
    public string? Fertigation { get; init; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (PlantingDate.HasValue && ExpectedHarvestDate.HasValue &&
            ExpectedHarvestDate.Value <= PlantingDate.Value)
        {
            yield return new ValidationResult(
                "Expected harvest date must be after the planting date.",
                [nameof(ExpectedHarvestDate)]);
        }

        if (ExpectedHarvestDate.HasValue && ExpectedExportDate.HasValue &&
            ExpectedExportDate.Value < ExpectedHarvestDate.Value)
        {
            yield return new ValidationResult(
                "Expected export date must be on or after the expected harvest date.",
                [nameof(ExpectedExportDate)]);
        }
    }
}

public sealed class ExportPlanFarmDataUpdateRequest
{
    [MaxLength(50)]
    public string? FarmType { get; init; }

    [MaxLength(50)]
    public string? SoilType { get; init; }

    [Range(0d, 14d)]
    public double? SoilPh { get; init; }

    [Range(0d, 100d)]
    public double? SoilEc { get; init; }

    [Range(0d, 100000d)]
    public double? SoilNitrogen { get; init; }

    [Range(0d, 100000d)]
    public double? SoilPhosphorus { get; init; }

    [Range(0d, 100000d)]
    public double? SoilPotassium { get; init; }

    [Range(0d, 100d)]
    public double? SoilOrganicMatter { get; init; }

    [Range(0d, 14d)]
    public double? WaterPh { get; init; }

    [Range(0d, 100d)]
    public double? WaterEc { get; init; }

    [Range(0d, 100000d)]
    public double? WaterSalinity { get; init; }

    [Range(0d, 100000d)]
    public double? WaterSodium { get; init; }

    [Range(0d, 100000d)]
    public double? WaterChloride { get; init; }

    [Range(0d, 100000d)]
    public double? WaterBicarbonate { get; init; }

    [Range(0d, 100000d)]
    public double? WaterCalcium { get; init; }

    [Range(0d, 100000d)]
    public double? WaterMagnesium { get; init; }

    [Range(0d, 1000d)]
    public double? WaterSar { get; init; }

    [MaxLength(50)]
    public string? IrrigationSystem { get; init; }

    [MaxLength(50)]
    public string? Fertigation { get; init; }
}

public sealed record SoilAnalysisSummaryDto(
    double? Ph,
    double? Ec,
    double? Nitrogen,
    double? Phosphorus,
    double? Potassium,
    double? OrganicMatter,
    bool IsCompleted);

public sealed record WaterAnalysisSummaryDto(
    double? Ph,
    double? Ec,
    double? Salinity,
    double? Sodium,
    double? Chloride,
    double? Bicarbonate,
    double? Calcium,
    double? Magnesium,
    double? Sar,
    bool IsCompleted);

public sealed record FarmDataSummaryDto(
    string? FarmType,
    string? SoilType,
    string? IrrigationSystem,
    string? Fertigation,
    SoilAnalysisSummaryDto SoilAnalysis,
    WaterAnalysisSummaryDto WaterAnalysis,
    bool IsFarmDataReady,
    int CompletedStepsCount,
    int TotalStepsCount);

public sealed class ExportPlanScheduleAnalysisRequest : IValidatableObject
{
    [Required, MaxLength(200)]
    public string? Name { get; init; }

    [Required]
    public int? CropId { get; init; }

    [Required]
    public int? DestinationId { get; init; }

    [Required, MaxLength(100)]
    public string? Governorate { get; init; }

    [Required, MaxLength(100)]
    public string? City { get; init; }

    [Required, Range(-90d, 90d)]
    public double? Latitude { get; init; }

    [Required, Range(-180d, 180d)]
    public double? Longitude { get; init; }

    [Required, Range(typeof(decimal), "0.01", "79228162514264337593543950335")]
    public decimal? AreaInFeddan { get; init; }

    [MaxLength(1000)]
    public string? Notes { get; init; }

    public DateOnly? PlantingDate { get; init; }
    public DateOnly? ExpectedHarvestDate { get; init; }
    public DateOnly? ExpectedExportDate { get; init; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        var suppliedDateCount = new[] { PlantingDate, ExpectedHarvestDate, ExpectedExportDate }
            .Count(date => date.HasValue);
        if (suppliedDateCount is > 0 and < 3)
        {
            yield return new ValidationResult(
                "Supply all three dates when re-evaluating a schedule.",
                [nameof(PlantingDate), nameof(ExpectedHarvestDate), nameof(ExpectedExportDate)]);
        }

        if (PlantingDate.HasValue && ExpectedHarvestDate <= PlantingDate)
        {
            yield return new ValidationResult(
                "Expected harvest date must be after the planting date.",
                [nameof(ExpectedHarvestDate)]);
        }

        if (ExpectedHarvestDate.HasValue && ExpectedExportDate < ExpectedHarvestDate)
        {
            yield return new ValidationResult(
                "Expected export date must be on or after the expected harvest date.",
                [nameof(ExpectedExportDate)]);
        }
    }
}

public sealed record ScheduleAnalysisSourceDto(
    string Id,
    string Title,
    string Publisher,
    string? Url,
    DateTime RetrievedAtUtc,
    string EvidenceKind,
    IReadOnlyList<string> SupportedFacts);

public sealed record ScheduleInferredFactDto(
    string Field,
    string Value,
    string Reason);

public sealed record ExportPlanScheduleAnalysisDto(
    string SeasonName,
    DateOnly PlantingDate,
    DateOnly ExpectedHarvestDate,
    DateOnly ExpectedExportDate,
    string Assessment,
    string Confidence,
    string Summary,
    IReadOnlyList<string> Reasons,
    IReadOnlyList<string> Warnings,
    IReadOnlyList<string> Recommendations,
    IReadOnlyList<string> Assumptions,
    IReadOnlyList<ScheduleAnalysisSourceDto> Sources,
    IReadOnlyList<ScheduleInferredFactDto> InferredFacts,
    string AnalysisToken);

public sealed record ExportScheduleAdvisorContext(
    string Crop,
    string? CropDescription,
    string Destination,
    string? DestinationDescription,
    string GovernorateDisplayName,
    string CityDisplayName,
    double Latitude,
    double Longitude,
    decimal AreaInFeddan,
    string? Notes,
    DateOnly Today,
    DateOnly? RequestedPlantingDate,
    DateOnly? RequestedHarvestDate,
    DateOnly? RequestedExportDate,
    IReadOnlyList<HistoricalExportScheduleDto> HistoricalSchedules,
    string? OwnerUserId = null,
    int? FarmId = null);

public sealed record HistoricalExportScheduleDto(
    DateOnly PlantingDate,
    DateOnly ExpectedHarvestDate,
    DateOnly ExpectedExportDate,
    string Status);

public sealed record ExportScheduleAdvisorResult(
    string SeasonName,
    DateOnly PlantingDate,
    DateOnly ExpectedHarvestDate,
    DateOnly ExpectedExportDate,
    string Assessment,
    string Confidence,
    string Summary,
    IReadOnlyList<string> Reasons,
    IReadOnlyList<string> Warnings,
    IReadOnlyList<string> Recommendations,
    IReadOnlyList<string> Assumptions,
    IReadOnlyList<ScheduleAnalysisSourceDto> Sources,
    IReadOnlyList<ScheduleInferredFactDto> InferredFacts);

public sealed record ExportPlanListItemDto(
    int Id,
    string Name,
    string Crop,
    string Destination,
    DateOnly PlantingDate,
    DateOnly ExpectedHarvestDate,
    DateOnly ExpectedExportDate,
    string Status,
    string PlanningStatus,
    int GeneratedTaskCount,
    bool IsFarmDataReady = false,
    string? FarmType = null,
    string? SoilType = null,
    string? IrrigationSystem = null);

public sealed record ExportPlanDetailsDto(
    int Id,
    string Name,
    int CropId,
    string Crop,
    bool SupportsSmartDiagnosis,
    int DestinationId,
    string Destination,
    string Governorate,
    string City,
    double Latitude,
    double Longitude,
    decimal AreaInFeddan,
    DateOnly PlantingDate,
    DateOnly ExpectedHarvestDate,
    DateOnly ExpectedExportDate,
    string Status,
    string? Notes,
    string PlanningStatus,
    int GeneratedTaskCount,
    string? PlanningFailureMessage,
    string? FarmType = null,
    string? SoilType = null,
    string? IrrigationSystem = null,
    string? Fertigation = null,
    SoilAnalysisSummaryDto? SoilAnalysis = null,
    WaterAnalysisSummaryDto? WaterAnalysis = null,
    bool IsFarmDataReady = false,
    int FarmDataCompletedCount = 0,
    bool IsSoilAnalysisCompleted = false,
    bool IsWaterAnalysisCompleted = false,
    double? SoilPh = null,
    double? SoilEc = null,
    double? SoilNitrogen = null,
    double? SoilPhosphorus = null,
    double? SoilPotassium = null,
    double? SoilOrganicMatter = null,
    double? WaterPh = null,
    double? WaterEc = null,
    double? WaterSalinity = null,
    double? WaterSodium = null,
    double? WaterChloride = null,
    double? WaterBicarbonate = null,
    double? WaterCalcium = null,
    double? WaterMagnesium = null,
    double? WaterSar = null);

public sealed record PlanningStatusDto(
    int ExportPlanId,
    string Status,
    int AttemptCount,
    int GeneratedTaskCount,
    DateTime? RequestedAtUtc,
    DateTime? StartedAtUtc,
    DateTime? CompletedAtUtc,
    string? FailureMessage);

public sealed record ExportPlanResult<T>(
    ExportPlanResultStatus Status,
    T? Value,
    string? ErrorField = null,
    string? ErrorMessage = null)
{
    public static ExportPlanResult<T> Success(T value) => new(ExportPlanResultStatus.Succeeded, value);

    public static ExportPlanResult<T> NotFound() => new(ExportPlanResultStatus.NotFound, default);

    public static ExportPlanResult<T> InvalidReference(string field, string message) =>
        new(ExportPlanResultStatus.InvalidReference, default, field, message);
}

public enum ExportPlanResultStatus
{
    Succeeded,
    NotFound,
    InvalidReference
}
