using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Configuration;

namespace Greenmind.Infrastructure.Iti;

public sealed class ItiBedrockGatewayClient : IItiBedrockGatewayClient
{
    public const string DefaultChatModel = "anthropic.claude-sonnet-4-6";
    public const string DefaultEmbeddingModel = "amazon.titan-embed-text-v2:0:8k";
    public const string DefaultVisionModel = DefaultChatModel;

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

        var apiKey = configuration["iti:ApiKey"];
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
        int maxTokens = 1200,
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
            system_prompt = NullIfWhiteSpace(systemPrompt),
            max_tokens = maxTokens
        };

        using var response = await _httpClient.PostAsJsonAsync(
            "student/chat",
            request,
            SerializerOptions,
            cancellationToken);
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

        using var response = await _httpClient.PostAsJsonAsync(
            "student/multimodal-chat",
            request,
            SerializerOptions,
            cancellationToken);
        using var document = await ReadSuccessfulJsonAsync(response, cancellationToken);
        return ReadOutputText(document.RootElement);
    }

    public async Task<IReadOnlyList<float[]>> EmbedAsync(
        IReadOnlyList<string> texts,
        string inputType,
        CancellationToken cancellationToken = default)
    {
        if (texts.Count == 0)
            return [];

        EnsureConfigured();

        var request = new
        {
            model_id = ResolveModel(null, "iti:EmbeddingModel", DefaultEmbeddingModel),
            texts,
            input_type = inputType
        };

        using var response = await _httpClient.PostAsJsonAsync(
            "student/embed",
            request,
            SerializerOptions,
            cancellationToken);
        using var document = await ReadSuccessfulJsonAsync(response, cancellationToken);
        return ReadEmbeddings(document.RootElement, texts.Count);
    }

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
                "The ITI gateway is not configured. Set iti:ApiKey using user-secrets or an environment variable.");
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

    private static IReadOnlyList<float[]> ReadEmbeddings(JsonElement root, int expectedCount)
    {
        JsonElement collection = default;
        foreach (var propertyName in new[] { "embeddings", "vectors", "data" })
        {
            if (root.TryGetProperty(propertyName, out collection) && collection.ValueKind == JsonValueKind.Array)
                break;
        }

        if (collection.ValueKind != JsonValueKind.Array)
            throw new InvalidDataException("ITI gateway response did not contain embeddings.");

        var result = new List<float[]>();
        foreach (var item in collection.EnumerateArray())
        {
            var vector = item;
            if (item.ValueKind == JsonValueKind.Object &&
                item.TryGetProperty("embedding", out var nestedVector))
            {
                vector = nestedVector;
            }

            if (vector.ValueKind != JsonValueKind.Array)
                throw new InvalidDataException("ITI gateway returned an invalid embedding vector.");

            result.Add(vector.EnumerateArray().Select(number => number.GetSingle()).ToArray());
        }

        if (result.Count != expectedCount)
        {
            throw new InvalidDataException(
                $"ITI gateway returned {result.Count} embeddings for {expectedCount} texts.");
        }

        return result;
    }
}
