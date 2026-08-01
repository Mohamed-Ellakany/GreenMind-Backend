using System.Net;
using System.Text;
using System.Text.Json;
using Greenmind.Infrastructure.Iti;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace Greenmind.Api.Tests;

public sealed class ItiGatewayClientTests
{
    [Fact]
    public async Task ChatAsync_UsesConfiguredModelBearerTokenAndOutputText()
    {
        var handler = new RecordingHandler(
            """{"output_text":"gateway reply"}""");
        var client = CreateClient(handler, new Dictionary<string, string?>
        {
            ["iti:ApiKey"] = "sbg_test_key",
            ["iti:Model"] = "anthropic.claude-sonnet-4-6"
        });

        var result = await client.ChatAsync(
            "Be concise.",
            [new ItiChatMessage("user", "Hello")],
            maxTokens: 321);

        Assert.Equal("gateway reply", result);
        Assert.Equal(
            "https://apiaccess.iti.net.eg/api/v1/student/chat",
            handler.RequestUri?.ToString());
        Assert.Equal("Bearer", handler.AuthorizationScheme);
        Assert.Equal("sbg_test_key", handler.AuthorizationParameter);

        using var body = JsonDocument.Parse(handler.RequestBody!);
        Assert.Equal("anthropic.claude-sonnet-4-6", body.RootElement.GetProperty("model_id").GetString());
        Assert.Equal(321, body.RootElement.GetProperty("max_tokens").GetInt32());
        Assert.Equal("Hello", body.RootElement.GetProperty("messages")[0].GetProperty("content").GetString());
    }

    [Fact]
    public async Task EmbedAsync_ParsesObjectWrappedEmbeddingVectors()
    {
        var handler = new RecordingHandler(
            """{"embeddings":[{"embedding":[0.25,-0.5]},{"embedding":[1.0,0.75]}]}""");
        var client = CreateClient(handler, new Dictionary<string, string?>
        {
            ["iti:ApiKey"] = "sbg_test_key"
        });

        var result = await client.EmbedAsync(["one", "two"], "search_document");

        Assert.Equal(2, result.Count);
        Assert.Equal([0.25f, -0.5f], result[0]);
        Assert.Equal([1.0f, 0.75f], result[1]);
        using var body = JsonDocument.Parse(handler.RequestBody!);
        Assert.Equal(
            ItiBedrockGatewayClient.DefaultEmbeddingModel,
            body.RootElement.GetProperty("model_id").GetString());
        Assert.Equal("search_document", body.RootElement.GetProperty("input_type").GetString());
    }

    [Fact]
    public async Task ChatAsync_ParsesNativeBedrockConverseEnvelope()
    {
        var handler = new RecordingHandler(
            """{"output":{"message":{"role":"assistant","content":[{"text":"nested reply"}]}}}""");
        var client = CreateClient(handler, new Dictionary<string, string?>
        {
            ["iti:ApiKey"] = "sbg_test_key"
        });

        var result = await client.ChatAsync(
            "Be concise.",
            [new ItiChatMessage("user", "Hello")]);

        Assert.Equal("nested reply", result);
    }

    [Fact]
    public async Task ChatAsync_WithoutApiKey_ReportsConfigurationErrorBeforeSending()
    {
        var handler = new RecordingHandler("""{"output_text":"unused"}""");
        var client = CreateClient(handler, new Dictionary<string, string?>());

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            client.ChatAsync(null, [new ItiChatMessage("user", "Hello")]));

        Assert.Contains("iti:ApiKey", exception.Message, StringComparison.Ordinal);
        Assert.Null(handler.RequestUri);
    }

    private static ItiBedrockGatewayClient CreateClient(
        RecordingHandler handler,
        IDictionary<string, string?> values)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(values)
            .Build();
        var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://apiaccess.iti.net.eg/api/v1/")
        };
        return new ItiBedrockGatewayClient(httpClient, configuration);
    }

    private sealed class RecordingHandler(string responseJson) : HttpMessageHandler
    {
        public Uri? RequestUri { get; private set; }
        public string? RequestBody { get; private set; }
        public string? AuthorizationScheme { get; private set; }
        public string? AuthorizationParameter { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            RequestUri = request.RequestUri;
            RequestBody = await request.Content!.ReadAsStringAsync(cancellationToken);
            AuthorizationScheme = request.Headers.Authorization?.Scheme;
            AuthorizationParameter = request.Headers.Authorization?.Parameter;

            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(responseJson, Encoding.UTF8, "application/json")
            };
        }
    }
}
