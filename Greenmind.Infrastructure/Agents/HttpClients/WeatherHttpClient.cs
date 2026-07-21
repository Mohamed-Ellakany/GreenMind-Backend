using Greenmind.Application.Dtos.AI;
using Greenmind.Application.Dtos.AI.WeatherApi;
using Greenmind.Application.Interfaces.AI;
using Microsoft.Extensions.Configuration;
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

    public async Task<WeatherForecastDto> GetForecastAsync(string city, DateOnly start, DateOnly end)
    {
        var days = Math.Max(1, (end.ToDateTime(TimeOnly.MinValue) - start.ToDateTime(TimeOnly.MinValue)).Days + 1);
        
        days = Math.Min(days, 14);

        var url = $"forecast.json?key={_apiKey}&q={Uri.EscapeDataString(city)}&days={days}";
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
