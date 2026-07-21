using System;
using System.Collections.Generic;
using System.Text;

namespace Greenmind.Application.Dtos.AI
{
    public class DailyWeatherDto
    {
        public DateOnly Date { get; set; }
        public double TempMinC { get; set; }
        public double TempMaxC { get; set; }
        public double PrecipitationMm { get; set; }
        public double HumidityPercent { get; set; }
        public double WindSpeedKmh { get; set; }
    }
}
