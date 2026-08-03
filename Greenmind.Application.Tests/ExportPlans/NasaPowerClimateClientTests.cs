using System.Net;
using Greenmind.Infrastructure.ExportPlans;
using Microsoft.Extensions.Caching.Memory;

namespace Greenmind.Application.Tests.ExportPlans;

public sealed class NasaPowerClimateClientTests
{
    [Fact]
    public async Task ClimateRequestsUseExactCoordinatesAndCacheByGridCell()
    {
        var handler = new RecordingHandler();
        var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://power.larc.nasa.gov/")
        };
        using var cache = new MemoryCache(new MemoryCacheOptions());
        var client = new NasaPowerClimateClient(httpClient, cache, TimeProvider.System);

        var first = await client.GetClimateAsync(29.912345, 30.923456);
        var sameGrid = await client.GetClimateAsync(29.91, 30.92);
        await client.GetClimateAsync(31.205432, 29.901234);

        Assert.Equal(2, handler.Requests.Count);
        Assert.Contains("latitude=29.912345", handler.Requests[0].Query);
        Assert.Contains("longitude=30.923456", handler.Requests[0].Query);
        Assert.Contains("latitude=31.205432", handler.Requests[1].Query);
        Assert.Contains("longitude=29.901234", handler.Requests[1].Query);
        Assert.Equal(29.91, sameGrid.Latitude);
        Assert.Equal(30.92, sameGrid.Longitude);
        Assert.Equal(12, first.Months.Count);
    }

    private sealed class RecordingHandler : HttpMessageHandler
    {
        public List<Uri> Requests { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            Requests.Add(request.RequestUri!);
            const string body = """
                {"properties":{"parameter":{
                  "T2M":{"01":15,"02":16,"03":18,"04":22,"05":26,"06":29,"07":30,"08":30,"09":28,"10":24,"11":20,"12":16},
                  "T2M_MIN":{"01":8},"T2M_MAX":{"01":22},
                  "PRECTOTCORR":{"01":5},"RH2M":{"01":60}
                }}}
                """;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(body)
            });
        }
    }
}
