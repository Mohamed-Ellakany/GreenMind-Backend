using System.Net;
using System.Text.Json;
using Microsoft.Extensions.Configuration;

namespace Greenmind.Application.Tests.ExportPlans;

public sealed class WeatherHttpClientTests
{
    [Fact]
    public async Task ForecastRequestUsesExactCoordinatesInsteadOfDisplayName()
    {
        var today = DateOnly.FromDateTime(DateTime.Today);
        var handler = new RecordingHandler(today);
        var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://api.weatherapi.com/v1/")
        };
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Weather:ApiKey"] = "test-key"
            })
            .Build();
        var client = new WeatherHttpClient(httpClient, configuration);

        await client.GetForecastAsync(29.912345, 30.923456, today, today);

        var request = Assert.Single(handler.Requests);
        Assert.Equal("29.912345,30.923456", request.Query.TrimStart('?')
            .Split('&')
            .Select(part => part.Split('=', 2))
            .Single(part => part[0] == "q")[1]
            .Replace("%2C", ",", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain("city", request.Query, StringComparison.OrdinalIgnoreCase);
    }

    private sealed class RecordingHandler(DateOnly date) : HttpMessageHandler
    {
        public List<Uri> Requests { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            Requests.Add(request.RequestUri!);
            var body = JsonSerializer.Serialize(new
            {
                forecast = new
                {
                    forecastday = new[]
                    {
                        new
                        {
                            date = date.ToString("yyyy-MM-dd"),
                            day = new
                            {
                                maxtemp_c = 25,
                                mintemp_c = 14,
                                avghumidity = 60,
                                totalprecip_mm = 0,
                                maxwind_kph = 12
                            }
                        }
                    }
                }
            });
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(body)
            });
        }
    }
}
