using Greenmind.Infrastructure.Agents;
using Xunit;

namespace Greenmind.Api.Tests;

public class WeatherConfigurationTests
{
    [Theory]
    [InlineData("https://api.weatherapi.com/v1")]
    [InlineData("https://api.weatherapi.com/v1/")]
    public void CreateWeatherBaseAddress_KeepsForecastRequestsUnderVersionPath(string baseUrl)
    {
        var baseAddress = KernelSetup.CreateWeatherBaseAddress(baseUrl);

        var forecastUri = new Uri(baseAddress, "forecast.json?key=test&q=30,31&days=7");

        Assert.Equal("https://api.weatherapi.com/v1/forecast.json?key=test&q=30,31&days=7", forecastUri.ToString());
    }
}
