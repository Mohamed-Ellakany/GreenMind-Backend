using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Configuration;

namespace Greenmind.Infrastructure.Iti;

public sealed class ItiBedrockGatewayClient : IItiBedrockGatewayClient
{
    private const int MaximumTransientAttempts = 3;
    public const string DefaultChatModel = "anthropic.claude-sonnet-4-6";
    public const string DefaultStructuredModel = "openai.gpt-oss-120b-1:0";
    public const string DefaultVisionModel = "anthropic.claude-haiku-4-5-20251001-v1:0";
    private static readonly Uri ChatReferer =
        new("http://apiaccess.iti.net.eg/student/playground");

    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private readonly HttpClient _httpClient;
    private readonly IConfiguration _configuration;
    private readonly bool _hasApiKey;

    public ItiBedrockGatewayClient(HttpClient httpClient, IConfiguration configuration)
    {
        _httpClient = httpClient;
        _configuration = configuration;

        // SBG_API_KEY is the gateway's documented environment variable. Keep
        // iti:ApiKey as a local/user-secrets fallback for existing deployments.
        var apiKey = Environment.GetEnvironmentVariable("SBG_API_KEY");
        if (string.IsNullOrWhiteSpace(apiKey))
            apiKey = configuration["iti:ApiKey"];

        if (!string.IsNullOrWhiteSpace(apiKey))
        {
            _httpClient.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", apiKey);
            _hasApiKey = true;
        }
    }

    public async Task<string> ChatAsync(
        string? systemPrompt,
        IReadOnlyList<ItiChatMessage> messages,
        string? modelId = null,
        CancellationToken cancellationToken = default)
    {
        EnsureConfigured();

        var request = new
        {
            model_id = ResolveModel(modelId, "iti:Model", DefaultChatModel),
            messages = messages.Select(message => new
            {
                role = message.Role,
                content = message.Content
            }),
            system_prompt = NullIfWhiteSpace(systemPrompt)
        };

        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, "student/chat")
        {
            Content = JsonContent.Create(request, options: SerializerOptions)
        };
        httpRequest.Headers.Referrer = ChatReferer;

        using var response = await _httpClient.SendAsync(httpRequest, cancellationToken);
        using var document = await ReadSuccessfulJsonAsync(response, cancellationToken);
        return ReadOutputText(document.RootElement);
    }

    public async Task<string> MultimodalChatAsync(
        string? systemPrompt,
        string text,
        IReadOnlyList<ItiImageInput> images,
        int maxTokens = 1200,
        string? modelId = null,
        CancellationToken cancellationToken = default)
    {
        EnsureConfigured();

        var combinedPrompt = string.IsNullOrWhiteSpace(systemPrompt)
            ? text
            : $"{systemPrompt}\n\n{text}";
        var request = new
        {
            model_id = ResolveModel(modelId, "iti:VisionModel", DefaultVisionModel),
            messages = new[]
            {
                new
                {
                    role = "user",
                    text = combinedPrompt,
                    images = images.Select(image => new
                    {
                        format = image.Format,
                        data_base64 = image.DataBase64
                    })
                }
            },
            max_tokens = maxTokens
        };

        using var response = await SendMultimodalAsync(request, cancellationToken);
        using var document = await ReadSuccessfulJsonAsync(response, cancellationToken);
        return ReadOutputText(document.RootElement);
    }

    private async Task<HttpResponseMessage> SendMultimodalAsync(
        object request,
        CancellationToken cancellationToken)
    {
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                var response = await _httpClient.PostAsJsonAsync(
                    "student/multimodal-chat",
                    request,
                    SerializerOptions,
                    cancellationToken);
                if (attempt == MaximumTransientAttempts || !IsTransient(response.StatusCode))
                    return response;

                response.Dispose();
            }
            catch (HttpRequestException) when (attempt < MaximumTransientAttempts)
            {
                // A connection failure can be temporary. The final exception is
                // allowed to propagate so the report service can log its details.
            }

            await Task.Delay(RetryDelay(attempt), cancellationToken);
        }
    }

    private static bool IsTransient(System.Net.HttpStatusCode statusCode) =>
        statusCode is System.Net.HttpStatusCode.RequestTimeout or
            System.Net.HttpStatusCode.TooManyRequests ||
        (int)statusCode >= 500;

    private static TimeSpan RetryDelay(int attempt) =>
        TimeSpan.FromMilliseconds(250 * attempt);

    private string ResolveModel(string? explicitModel, string configurationKey, string fallback)
    {
        if (!string.IsNullOrWhiteSpace(explicitModel))
            return explicitModel;

        var configured = _configuration[configurationKey];
        return string.IsNullOrWhiteSpace(configured) ? fallback : configured;
    }

    private void EnsureConfigured()
    {
        if (!_hasApiKey)
        {
            throw new InvalidOperationException(
                "The ITI gateway is not configured. Set SBG_API_KEY or iti:ApiKey using user-secrets.");
        }
    }

    private static string? NullIfWhiteSpace(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value;

    private static async Task<JsonDocument> ReadSuccessfulJsonAsync(
        HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw new HttpRequestException(
                $"ITI gateway returned {(int)response.StatusCode} ({response.ReasonPhrase}): {body}",
                null,
                response.StatusCode);
        }

        try
        {
            return JsonDocument.Parse(body);
        }
        catch (JsonException exception)
        {
            throw new InvalidDataException("ITI gateway returned invalid JSON.", exception);
        }
    }

    internal static string ReadOutputText(JsonElement root)
    {
        if (TryReadOutputText(root, out var output))
            return output;

        throw new InvalidDataException("ITI gateway response did not contain model output text.");
    }

    private static bool TryReadOutputText(JsonElement element, out string output)
    {
        if (element.ValueKind == JsonValueKind.String)
        {
            output = element.GetString() ?? string.Empty;
            return !string.IsNullOrWhiteSpace(output);
        }

        if (element.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in element.EnumerateArray())
            {
                if (TryReadOutputText(item, out output))
                    return true;
            }
        }

        if (element.ValueKind == JsonValueKind.Object)
        {
            string[] directTextProperties =
                ["output_text", "generated_text", "text", "answer", "content"];
            foreach (var propertyName in directTextProperties)
            {
                if (element.TryGetProperty(propertyName, out var value) &&
                    value.ValueKind == JsonValueKind.String &&
                    !string.IsNullOrWhiteSpace(value.GetString()))
                {
                    output = value.GetString()!;
                    return true;
                }
            }

            // Support the common gateway envelopes as well as the native Bedrock
            // Converse response: output.message.content[].text.
            string[] envelopeProperties =
                ["data", "response", "result", "output", "message", "content"];
            foreach (var propertyName in envelopeProperties)
            {
                if (element.TryGetProperty(propertyName, out var value) &&
                    TryReadOutputText(value, out output))
                {
                    return true;
                }
            }
        }

        output = string.Empty;
        return false;
    }

}
