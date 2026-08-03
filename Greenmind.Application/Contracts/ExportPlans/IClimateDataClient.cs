namespace Greenmind.Application.Contracts.ExportPlans;

public interface IClimateDataClient
{
    Task<ClimateDataSnapshot> GetClimateAsync(
        double latitude,
        double longitude,
        CancellationToken cancellationToken = default);
}

public sealed record ClimateMonthSummary(
    int Month,
    double? MeanTemperatureC,
    double? MinimumTemperatureC,
    double? MaximumTemperatureC,
    double? PrecipitationMm,
    double? RelativeHumidityPercent);

public sealed record ClimateDataSnapshot(
    double Latitude,
    double Longitude,
    IReadOnlyList<ClimateMonthSummary> Months,
    DateTime RetrievedAtUtc,
    string SourceTitle,
    string SourceUrl);
