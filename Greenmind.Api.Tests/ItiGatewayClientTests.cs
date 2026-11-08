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
    public async Task ChatAsync_UsesStudentChatSchemaHeadersAndOutputText()
    {
        var handler = new RecordingHandler(
            """{"output_text":"gateway reply"}""");
        var client = CreateClient(handler, new Dictionary<string, string?>
        {
            ["iti:ApiKey"] = "sbg_test_key",
            ["iti:Model"] = "qwen.qwen3-vl-235b-a22b"
        });

        var result = await client.ChatAsync(
            "Be concise.",
            [new ItiChatMessage("user", "Hello")]);

        Assert.Equal("gateway reply", result);
        Assert.Equal(
            "http://apiaccess.iti.net.eg/api/v1/student/chat",
            handler.RequestUri?.ToString());
        Assert.Equal(
            "http://apiaccess.iti.net.eg/student/playground",
            handler.Referrer?.ToString());
        Assert.Equal("Bearer", handler.AuthorizationScheme);
        Assert.Equal("sbg_test_key", handler.AuthorizationParameter);

        using var body = JsonDocument.Parse(handler.RequestBody!);
        Assert.Equal(3, body.RootElement.EnumerateObject().Count());
        Assert.Equal("qwen.qwen3-vl-235b-a22b", body.RootElement.GetProperty("model_id").GetString());
        Assert.Equal("Be concise.", body.RootElement.GetProperty("system_prompt").GetString());
        Assert.Equal("user", body.RootElement.GetProperty("messages")[0].GetProperty("role").GetString());
        Assert.Equal("Hello", body.RootElement.GetProperty("messages")[0].GetProperty("content").GetString());
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

        Assert.Contains("SBG_API_KEY", exception.Message, StringComparison.Ordinal);
        Assert.Null(handler.RequestUri);
    }

    [Fact]
    public async Task ChatAsync_RetriesTransientGatewayFailures()
    {
        var handler = new RecordingHandler(
            """{"output_text":"recovered reply"}""",
            HttpStatusCode.ServiceUnavailable,
            HttpStatusCode.TooManyRequests,
            HttpStatusCode.OK);
        var client = CreateClient(handler, new Dictionary<string, string?>
        {
            ["iti:ApiKey"] = "sbg_test_key"
        });

        var result = await client.ChatAsync(
            "Be concise.",
            [new ItiChatMessage("user", "Hello")]);

        Assert.Equal("recovered reply", result);
        Assert.Equal(3, handler.RequestCount);
    }

    [Fact]
    public async Task MultimodalChatAsync_UsesRequiredImageSchemaAndConfiguredBearerToken()
    {
        var handler = new RecordingHandler("""{"output_text":"image reply"}""");
        var client = CreateClient(handler, new Dictionary<string, string?>
        {
            ["iti:ApiKey"] = "sbg_test_key"
        });

        var result = await client.MultimodalChatAsync(
            "Be concise.",
            "Describe this image for a beginner.",
            [new ItiImageInput("png", "BASE64_IMAGE")],
            maxTokens: 300);

        Assert.Equal("image reply", result);
        Assert.Equal(
            "http://apiaccess.iti.net.eg/api/v1/student/multimodal-chat",
            handler.RequestUri?.ToString());
        Assert.Equal("Bearer", handler.AuthorizationScheme);
        Assert.Equal("sbg_test_key", handler.AuthorizationParameter);

        using var body = JsonDocument.Parse(handler.RequestBody!);
        Assert.Equal("qwen.qwen3-vl-235b-a22b", body.RootElement.GetProperty("model_id").GetString());
        Assert.Equal(300, body.RootElement.GetProperty("max_tokens").GetInt32());
        var message = body.RootElement.GetProperty("messages")[0];
        Assert.Equal("user", message.GetProperty("role").GetString());
        Assert.Equal("Be concise.\n\nDescribe this image for a beginner.", message.GetProperty("text").GetString());
        var image = message.GetProperty("images")[0];
        Assert.Equal("png", image.GetProperty("format").GetString());
        Assert.Equal("BASE64_IMAGE", image.GetProperty("data_base64").GetString());
    }

    [Fact]
    public async Task MultimodalChatAsync_UsesConfiguredVisionModel()
    {
        var handler = new RecordingHandler("""{"output_text":"image reply"}""");
        var client = CreateClient(handler, new Dictionary<string, string?>
        {
            ["iti:ApiKey"] = "sbg_test_key",
            ["iti:VisionModel"] = "qwen.qwen3-vl-235b-a22b"
        });

        await client.MultimodalChatAsync(
            null,
            "Describe the image.",
            [new ItiImageInput("png", "BASE64_IMAGE")]);

        using var body = JsonDocument.Parse(handler.RequestBody!);
        Assert.Equal(
            "qwen.qwen3-vl-235b-a22b",
            body.RootElement.GetProperty("model_id").GetString());
    }

    [Fact]
    public async Task MultimodalChatAsync_RetriesTransientGatewayFailures()
    {
        var handler = new RecordingHandler(
            """{"output_text":"image reply"}""",
            HttpStatusCode.ServiceUnavailable,
            HttpStatusCode.TooManyRequests,
            HttpStatusCode.OK);
        var client = CreateClient(handler, new Dictionary<string, string?>
        {
            ["iti:ApiKey"] = "sbg_test_key"
        });

        var result = await client.MultimodalChatAsync(
            null,
            "Describe the image.",
            [new ItiImageInput("png", "BASE64_IMAGE")]);

        Assert.Equal("image reply", result);
        Assert.Equal(3, handler.RequestCount);
    }

    [Fact]
    public async Task MultimodalChatAsync_DoesNotRetryPermanentGatewayFailures()
    {
        var handler = new RecordingHandler(
            """{"detail":"model is not allowed"}""",
            HttpStatusCode.Forbidden);
        var client = CreateClient(handler, new Dictionary<string, string?>
        {
            ["iti:ApiKey"] = "sbg_test_key"
        });

        var exception = await Assert.ThrowsAsync<HttpRequestException>(() =>
            client.MultimodalChatAsync(
                null,
                "Describe the image.",
                [new ItiImageInput("png", "BASE64_IMAGE")]));

        Assert.Equal(HttpStatusCode.Forbidden, exception.StatusCode);
        Assert.Equal(1, handler.RequestCount);
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
            BaseAddress = new Uri("http://apiaccess.iti.net.eg/api/v1/")
        };
        return new ItiBedrockGatewayClient(httpClient, configuration);
    }

    private sealed class RecordingHandler(
        string responseJson,
        params HttpStatusCode[] responseStatuses) : HttpMessageHandler
    {
        private readonly Queue<HttpStatusCode> _responseStatuses = new(responseStatuses);

        public int RequestCount { get; private set; }
        public Uri? RequestUri { get; private set; }
        public string? RequestBody { get; private set; }
        public Uri? Referrer { get; private set; }
        public string? AuthorizationScheme { get; private set; }
        public string? AuthorizationParameter { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            RequestCount++;
            RequestUri = request.RequestUri;
            RequestBody = await request.Content!.ReadAsStringAsync(cancellationToken);
            Referrer = request.Headers.Referrer;
            AuthorizationScheme = request.Headers.Authorization?.Scheme;
            AuthorizationParameter = request.Headers.Authorization?.Parameter;

            return new HttpResponseMessage(
                _responseStatuses.Count > 0
                    ? _responseStatuses.Dequeue()
                    : HttpStatusCode.OK)
            {
                Content = new StringContent(responseJson, Encoding.UTF8, "application/json")
            };
        }
    }
}
