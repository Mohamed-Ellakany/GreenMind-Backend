
using System.Text.Json.Serialization;

namespace Greenmind.Application.Dtos.AI.WeatherApi
{
    public class WeatherApiRawResponse
    {
        public ForecastRoot Forecast { get; set; } = null!;
    }
    public class ForecastRoot
    {
        public List<ForecastDay> Forecastday { get; set; } = new();
    }
    public class ForecastDay
    {
        public string Date { get; set; } = null!;
        public ForecastDayDetails Day { get; set; } = null!;
    }
    public class ForecastDayDetails
    {
        public double Maxtemp_C { get; set; }
        public double Mintemp_C { get; set; }
        public double Avghumidity { get; set; }
        public double Totalprecip_Mm { get; set; }
        public double Maxwind_Kph { get; set; }
    }
}

