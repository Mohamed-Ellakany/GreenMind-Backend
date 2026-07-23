using System.ComponentModel.DataAnnotations;

namespace Greenmind.Application.Dtos.Soil;

public sealed record SoilMeasurementDto(
    int Id,
    DateOnly MeasurementDate,
    double Ph,
    double Ec,
    double Nitrogen,
    double Phosphorus,
    double Potassium,
    double Moisture,
    string? Notes,
    DateTime CreatedAt);

public sealed class SoilMeasurementRequest
{
    [Required]
    public DateOnly? MeasurementDate { get; init; }

    [Required]
    [Range(0d, 14d)]
    public double? Ph { get; init; }

    [Required]
    [Range(0, double.MaxValue)]
    public double? Ec { get; init; }

    [Required]
    [Range(0, double.MaxValue)]
    public double? Nitrogen { get; init; }

    [Required]
    [Range(0, double.MaxValue)]
    public double? Phosphorus { get; init; }

    [Required]
    [Range(0, double.MaxValue)]
    public double? Potassium { get; init; }

    [Required]
    [Range(0, double.MaxValue)]
    public double? Moisture { get; init; }

    [MaxLength(500)]
    public string? Notes { get; init; }
}

public sealed record SoilMeasurementResult<T>(
    bool Found,
    T? Value)
{
    public static SoilMeasurementResult<T> Success(T? value) => new(true, value);

    public static SoilMeasurementResult<T> NotFound() => new(false, default);
}
