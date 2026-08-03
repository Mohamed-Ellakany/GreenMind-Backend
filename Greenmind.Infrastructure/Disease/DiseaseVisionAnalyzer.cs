using System.Text.Json;
using System.Text.Json.Serialization;
using Greenmind.Application.Contracts.Disease;
using Greenmind.Application.Dtos.Disease;
using Greenmind.Domain.Consts.Enums;
using Greenmind.Infrastructure.Iti;
using Microsoft.Extensions.Configuration;

namespace Greenmind.Infrastructure.Disease;

public sealed class DiseaseVisionAnalyzer : IDiseaseVisionAnalyzer
{
    private const string PromptVersion = "disease-v2";
    private const int MaxTokens = 1800;
    private const string ResponseSchema =
        """
        Return exactly one JSON object with all of these properties and no markdown:
        {
          "suspectedDisease": "Arabic text",
          "confidencePercentage": 0,
          "severity": "low | medium | high | critical",
          "observedSymptoms": "Arabic text",
          "recommendedActions": "Arabic text",
          "exportImpact": "Arabic text",
          "advisoryWarning": "Arabic text"
        }
        Never rename or omit a property. Use an Arabic explanatory value when a descriptive
        value cannot be determined; confidencePercentage must be a JSON number.
        """;
    private readonly IItiBedrockGatewayClient _gatewayClient;
    private readonly string _modelName;

    public DiseaseVisionAnalyzer(
        IConfiguration configuration,
        IItiBedrockGatewayClient gatewayClient)
    {
        _modelName = string.IsNullOrWhiteSpace(configuration["iti:VisionModel"])
            ? ItiBedrockGatewayClient.DefaultVisionModel
            : configuration["iti:VisionModel"]!;
        _gatewayClient = gatewayClient;
    }

    public async Task<DiseaseVisionResult> AnalyzeAsync(
        DiseaseVisionRequest request,
        CancellationToken cancellationToken = default)
    {
        var systemPrompt =
            """
            أنت مساعد زراعي استشاري متخصص في تحليل صور أعراض النبات.
            لا تدّعِ تشخيصًا معمليًا مؤكدًا، ولا توصي بجرعات مبيدات أو مواد محظورة.
            اكتب جميع الحقول الوصفية بالعربية. إذا كانت الصور غير واضحة أو الثقة منخفضة،
            اطلب صورًا أوضح وفحصًا متخصصًا. إذا كانت الشدة عالية أو حرجة، أوصِ بفحص متخصص.
            أعد كائن JSON فقط مطابقًا للمخطط المطلوب.
            """;

        var userPrompt = $"""
            حلل صور النبات كتقييم استشاري غير نهائي.
            تاريخ الملاحظة: {request.ObservationDate:yyyy-MM-dd}
            المنطقة المتأثرة: {request.AffectedArea ?? "غير محددة"}
            ملاحظات المزارع: {request.Notes ?? "لا توجد"}

            سياق خطة التصدير:
            {request.PlanContext}

            قيم severity المسموحة: low, medium, high, critical.
            confidencePercentage رقم من 0 إلى 100.
            """;
        var images = request.Images
            .Select(image => new ItiImageInput(
                ToImageFormat(image.ContentType),
                Convert.ToBase64String(image.Content)))
            .ToList();
        var response = await AnalyzeImagesAsync(
            systemPrompt,
            $"{userPrompt}\n\n{ResponseSchema}",
            images,
            cancellationToken);
        DiseaseAnalysisPayload payload;
        try
        {
            payload = DeserializePayload(response);
        }
        catch (Exception exception) when (exception is JsonException or InvalidDataException)
        {
            var correctionPrompt = $"""
                Your previous response did not match the required JSON contract. Analyze the
                same images again and return a complete replacement response.

                {ResponseSchema}
                """;
            response = await AnalyzeImagesAsync(
                systemPrompt,
                $"{userPrompt}\n\n{correctionPrompt}",
                images,
                cancellationToken);

            try
            {
                payload = DeserializePayload(response);
            }
            catch (Exception retryException) when (retryException is JsonException or InvalidDataException)
            {
                throw new InvalidDataException(
                    "Disease model returned an invalid structured result after retry.",
                    retryException);
            }
        }

        return new DiseaseVisionResult(
            payload.SuspectedDisease,
            payload.ConfidencePercentage,
            ParseSeverity(payload.Severity),
            payload.ObservedSymptoms,
            payload.RecommendedActions,
            payload.ExportImpact,
            payload.AdvisoryWarning,
            _modelName,
            PromptVersion);
    }

    private Task<string> AnalyzeImagesAsync(
        string systemPrompt,
        string userPrompt,
        IReadOnlyList<ItiImageInput> images,
        CancellationToken cancellationToken) =>
        _gatewayClient.MultimodalChatAsync(
            systemPrompt,
            userPrompt,
            images,
            maxTokens: MaxTokens,
            modelId: _modelName,
            cancellationToken: cancellationToken);

    private static DiseaseAnalysisPayload DeserializePayload(string response) =>
        JsonSerializer.Deserialize<DiseaseAnalysisPayload>(
            ItiJson.ExtractObject(response),
            new JsonSerializerOptions(JsonSerializerDefaults.Web))
        ?? throw new InvalidDataException("Disease model returned no structured result.");

    private static string ToImageFormat(string contentType) =>
        contentType.ToLowerInvariant() switch
        {
            "image/jpeg" => "jpeg",
            "image/png" => "png",
            "image/webp" => "webp",
            "image/gif" => "gif",
            _ => throw new InvalidDataException($"Unsupported image type: {contentType}")
        };

    private static DiseaseSeverity ParseSeverity(string value) =>
        value.Trim().ToLowerInvariant() switch
        {
            "low" => DiseaseSeverity.Low,
            "medium" => DiseaseSeverity.Medium,
            "high" => DiseaseSeverity.High,
            "critical" => DiseaseSeverity.Critical,
            _ => throw new InvalidDataException("Disease model returned an invalid severity.")
        };

    private sealed record DiseaseAnalysisPayload
    {
        [JsonPropertyName("suspectedDisease")]
        public required string SuspectedDisease { get; init; }

        [JsonPropertyName("confidencePercentage")]
        public required decimal ConfidencePercentage { get; init; }

        [JsonPropertyName("severity")]
        public required string Severity { get; init; }

        [JsonPropertyName("observedSymptoms")]
        public required string ObservedSymptoms { get; init; }

        [JsonPropertyName("recommendedActions")]
        public required string RecommendedActions { get; init; }

        [JsonPropertyName("exportImpact")]
        public required string ExportImpact { get; init; }

        [JsonPropertyName("advisoryWarning")]
        public required string AdvisoryWarning { get; init; }
    }
}
