
using System.Text.Json.Serialization;

namespace Greenmind.Application.Dtos.AI.WeatherApi
{
    public class WeatherApiRawResponse
    {
        [JsonPropertyName("forecast")]
        public ForecastRoot Forecast { get; set; } = null!;
    }
    public class ForecastRoot
    {
        [JsonPropertyName("forecastday")]
        public List<ForecastDay> Forecastday { get; set; } = new();
    }
    public class ForecastDay
    {
        [JsonPropertyName("date")]
        public string Date { get; set; } = null!;

        [JsonPropertyName("day")]
        public ForecastDayDetails Day { get; set; } = null!;
    }
    public class ForecastDayDetails
    {
        [JsonPropertyName("maxtemp_c")]
        public double Maxtemp_C { get; set; }

        [JsonPropertyName("mintemp_c")]
        public double Mintemp_C { get; set; }

        [JsonPropertyName("avghumidity")]
        public double Avghumidity { get; set; }

        [JsonPropertyName("totalprecip_mm")]
        public double Totalprecip_Mm { get; set; }

        [JsonPropertyName("maxwind_kph")]
        public double Maxwind_Kph { get; set; }
    }
}

