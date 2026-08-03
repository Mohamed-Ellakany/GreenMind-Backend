using Greenmind.Application.Interfaces.AI;
using Microsoft.SemanticKernel;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Text;
using System.Text.Json;

namespace Greenmind.Infrastructure.Agents.Plugins
{
    public class WeatherPlugin
    {
        private readonly IWeatherHttpClient _client;
        public WeatherPlugin(IWeatherHttpClient client) => _client = client;

        [KernelFunction("get_weather_forecast")]
        [Description("Gets daily weather forecast (temp, rain, humidity, wind) for exact farm coordinates between two dates.")]
        public async Task<string> GetForecastAsync(
            [Description("Farm latitude")] double latitude,
            [Description("Farm longitude")] double longitude,
            [Description("Start date yyyy-MM-dd")] string startDate,
            [Description("End date yyyy-MM-dd")] string endDate)
        {
            var forecast = await _client.GetForecastAsync(
                latitude, longitude, DateOnly.Parse(startDate), DateOnly.Parse(endDate));
            return JsonSerializer.Serialize(forecast);
        }
    }
}
