using Greenmind.Application.Dtos.AI;
using System;
using System.Collections.Generic;
using System.Text;

namespace Greenmind.Application.Interfaces.AI
{
    public interface IWeatherHttpClient
    {
        Task<WeatherForecastDto> GetForecastAsync(string city, DateOnly start, DateOnly end);

    }
}
