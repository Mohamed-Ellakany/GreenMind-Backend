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

    [Required]
    public ExportPlanStatus? Status { get; init; }

    [MaxLength(1000)]
    public string? Notes { get; init; }

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

public sealed record ExportPlanListItemDto(
    int Id,
    string Name,
    string Crop,
    string Destination,
    DateOnly PlantingDate,
    DateOnly ExpectedHarvestDate,
    DateOnly ExpectedExportDate,
    string Status);

public sealed record ExportPlanDetailsDto(
    int Id,
    string Name,
    int CropId,
    string Crop,
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
    string? Notes);

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
