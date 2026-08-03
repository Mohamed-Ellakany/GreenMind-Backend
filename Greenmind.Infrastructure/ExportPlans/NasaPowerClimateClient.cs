using System.Globalization;
using System.Net.Http.Json;
using System.Text.Json;
using Greenmind.Application.Contracts.ExportPlans;
using Microsoft.Extensions.Caching.Memory;

namespace Greenmind.Infrastructure.ExportPlans;

public sealed class NasaPowerClimateClient(
    HttpClient httpClient,
    IMemoryCache cache,
    TimeProvider timeProvider) : IClimateDataClient
{
    private const string SourceTitle = "NASA POWER Agroclimatology";
    private const string SourceUrl = "https://power.larc.nasa.gov/docs/services/api/temporal/climatology/";

    public async Task<ClimateDataSnapshot> GetClimateAsync(
        double latitude,
        double longitude,
        CancellationToken cancellationToken = default)
    {
        var gridLatitude = Math.Round(latitude * 2, MidpointRounding.AwayFromZero) / 2;
        var gridLongitude = Math.Round(longitude * 2, MidpointRounding.AwayFromZero) / 2;
        var cacheKey = $"nasa-power:{gridLatitude:F1}:{gridLongitude:F1}";

        if (cache.TryGetValue(cacheKey, out ClimateDataSnapshot? cached) && cached is not null)
            return cached with { Latitude = latitude, Longitude = longitude };

        var query = string.Create(
            CultureInfo.InvariantCulture,
            $"api/temporal/climatology/point?parameters=T2M,T2M_MIN,T2M_MAX,PRECTOTCORR,RH2M&community=AG&longitude={longitude:F6}&latitude={latitude:F6}&start=1991&end=2020&format=JSON");

        using var response = await httpClient.GetAsync(query, cancellationToken);
        response.EnsureSuccessStatusCode();
        using var document = await JsonDocument.ParseAsync(
            await response.Content.ReadAsStreamAsync(cancellationToken),
            cancellationToken: cancellationToken);

        var parameter = document.RootElement.GetProperty("properties").GetProperty("parameter");
        var months = Enumerable.Range(1, 12)
            .Select(month => new ClimateMonthSummary(
                month,
                ReadMonth(parameter, "T2M", month),
                ReadMonth(parameter, "T2M_MIN", month),
                ReadMonth(parameter, "T2M_MAX", month),
                ReadMonth(parameter, "PRECTOTCORR", month),
                ReadMonth(parameter, "RH2M", month)))
            .ToArray();

        var result = new ClimateDataSnapshot(
            latitude,
            longitude,
            months,
            timeProvider.GetUtcNow().UtcDateTime,
            SourceTitle,
            SourceUrl);
        cache.Set(cacheKey, result, TimeSpan.FromHours(24));
        return result;
    }

    private static double? ReadMonth(JsonElement parameters, string parameterName, int month)
    {
        if (!parameters.TryGetProperty(parameterName, out var values)) return null;
        var key = month.ToString("D2", CultureInfo.InvariantCulture);
        if (!values.TryGetProperty(key, out var value) || value.ValueKind != JsonValueKind.Number) return null;
        var number = value.GetDouble();
        return number <= -900 ? null : number;
    }
}
