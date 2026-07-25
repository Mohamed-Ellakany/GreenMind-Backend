using System.Text.Json;
using Greenmind.Application.Dtos.AI.WeatherApi;

namespace Greenmind.Application.Tests.AI;

public class WeatherApiResponseTests
{
    [Fact]
    public void WeatherApiResponse_DeserializesSnakeCaseWeatherFields()
    {
        const string json = """
        {
          "forecast": {
            "forecastday": [
              {
                "date": "2026-07-25",
                "day": {
                  "maxtemp_c": 34.5,
                  "mintemp_c": 23.1,
                  "avghumidity": 61,
                  "totalprecip_mm": 4.2,
                  "maxwind_kph": 18.7
                }
              }
            ]
          }
        }
        """;

        var response = JsonSerializer.Deserialize<WeatherApiRawResponse>(json);
        var day = response!.Forecast.Forecastday.Single().Day;

        Assert.Equal(34.5, day.Maxtemp_C);
        Assert.Equal(23.1, day.Mintemp_C);
        Assert.Equal(61, day.Avghumidity);
        Assert.Equal(4.2, day.Totalprecip_Mm);
        Assert.Equal(18.7, day.Maxwind_Kph);
    }
}
