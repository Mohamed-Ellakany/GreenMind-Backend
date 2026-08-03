using Greenmind.Application.Dtos.AI;
using Greenmind.Application.Dtos.AI.WeatherApi;
using Greenmind.Application.Interfaces.AI;
using Microsoft.Extensions.Configuration;
using System.Globalization;
using System.Net.Http.Json;

public class WeatherHttpClient : IWeatherHttpClient
{
    private readonly HttpClient _http;
    private readonly string _apiKey;

    public WeatherHttpClient(HttpClient http, IConfiguration config)
    {
        _http = http;
        _apiKey = config["Weather:ApiKey"]!;
    }

    public async Task<WeatherForecastDto> GetForecastAsync(
        double latitude,
        double longitude,
        DateOnly start,
        DateOnly end)
    {
        if (end < start)
        {
            throw new ArgumentException("Forecast end date must be on or after the start date.", nameof(end));
        }

        var today = DateOnly.FromDateTime(DateTime.Today);
        var lastForecastDate = end < today ? today : end;
        var days = Math.Max(1, lastForecastDate.DayNumber - today.DayNumber + 1);

        days = Math.Min(days, 14);

        var coordinates = string.Create(
            CultureInfo.InvariantCulture,
            $"{latitude},{longitude}");
        var url = $"forecast.json?key={_apiKey}&q={Uri.EscapeDataString(coordinates)}&days={days}";
        var resp = await _http.GetAsync(url);
        resp.EnsureSuccessStatusCode();

        var raw = await resp.Content.ReadFromJsonAsync<WeatherApiRawResponse>();
        return MapToForecastDto(raw!, start, end);
    }

    private static WeatherForecastDto MapToForecastDto(WeatherApiRawResponse raw, DateOnly start, DateOnly end)
    {
        var days = raw.Forecast.Forecastday
            .Where(d => DateOnly.Parse(d.Date) >= start && DateOnly.Parse(d.Date) <= end)
            .Select(d => new DailyWeatherDto
            {
                Date = DateOnly.Parse(d.Date),
                TempMinC = d.Day.Mintemp_C,
                TempMaxC = d.Day.Maxtemp_C,
                PrecipitationMm = d.Day.Totalprecip_Mm,
                HumidityPercent = d.Day.Avghumidity,
                WindSpeedKmh = d.Day.Maxwind_Kph
            }).ToList();

        return new WeatherForecastDto { Days = days };
    }
}
