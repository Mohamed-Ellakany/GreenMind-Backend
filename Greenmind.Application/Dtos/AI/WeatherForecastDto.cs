using System;
using System.Collections.Generic;
using System.Text;

namespace Greenmind.Application.Dtos.AI
{
    public class WeatherForecastDto
    {
        public List<DailyWeatherDto> Days { get; set; } = new();
    }
}
