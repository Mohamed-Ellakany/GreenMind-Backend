using Greenmind.Application.Dtos.AI;
using System;
using System.Collections.Generic;
using System.Text;

namespace Greenmind.Application.Interfaces.AI
{
    public interface IWeatherHttpClient
    {
        Task<WeatherForecastDto> GetForecastAsync(double latitude, double longitude, DateOnly start, DateOnly end);

    }
}
