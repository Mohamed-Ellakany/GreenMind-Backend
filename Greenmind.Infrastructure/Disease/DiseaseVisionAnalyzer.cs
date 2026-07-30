using System.Text.Json;
using System.Text.Json.Serialization;
using Greenmind.Application.Contracts.Disease;
using Greenmind.Application.Dtos.Disease;
using Greenmind.Domain.Consts.Enums;
using Microsoft.Extensions.Configuration;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.OpenAI;

namespace Greenmind.Infrastructure.Disease;

public sealed class DiseaseVisionAnalyzer : IDiseaseVisionAnalyzer
{
    private const string PromptVersion = "disease-v1";
    private readonly IChatCompletionService _chatCompletion;
    private readonly string _modelName;

    public DiseaseVisionAnalyzer(IConfiguration configuration)
    {
        _modelName = configuration["DiseaseAnalysis:Model"] ?? "openai/gpt-4.1";
        var endpoint = new Uri(
            configuration["OpenAI:Endpoint"] ?? "https://models.github.ai/inference");
        var builder = Kernel.CreateBuilder();
        builder.AddOpenAIChatCompletion(
            modelId: _modelName,
            apiKey: configuration["OpenAI:ApiKey"] ?? string.Empty,
            endpoint: endpoint);
        _chatCompletion = builder.Build()
            .GetRequiredService<IChatCompletionService>();
    }

    public async Task<DiseaseVisionResult> AnalyzeAsync(
        DiseaseVisionRequest request,
        CancellationToken cancellationToken = default)
    {
        var history = new ChatHistory(
            """
            أنت مساعد زراعي استشاري متخصص في تحليل صور أعراض النبات.
            لا تدّعِ تشخيصًا معمليًا مؤكدًا، ولا توصي بجرعات مبيدات أو مواد محظورة.
            اكتب جميع الحقول الوصفية بالعربية. إذا كانت الصور غير واضحة أو الثقة منخفضة،
            اطلب صورًا أوضح وفحصًا متخصصًا. إذا كانت الشدة عالية أو حرجة، أوصِ بفحص متخصص.
            أعد كائن JSON فقط مطابقًا للمخطط المطلوب.
            """);

        var items = new ChatMessageContentItemCollection
        {
            new TextContent($"""
            حلل صور النبات كتقييم استشاري غير نهائي.
            تاريخ الملاحظة: {request.ObservationDate:yyyy-MM-dd}
            المنطقة المتأثرة: {request.AffectedArea ?? "غير محددة"}
            ملاحظات المزارع: {request.Notes ?? "لا توجد"}

            سياق خطة التصدير:
            {request.PlanContext}

            قيم severity المسموحة: low, medium, high, critical.
            confidencePercentage رقم من 0 إلى 100.
            """)
        };
        foreach (var image in request.Images)
        {
            items.Add(new ImageContent(
                image.Content,
                image.ContentType));
        }
        var userMessage = new ChatMessageContent(AuthorRole.User, items);
        history.Add(userMessage);

        var settings = new OpenAIPromptExecutionSettings
        {
            FunctionChoiceBehavior = FunctionChoiceBehavior.None(),
            Temperature = 0.1,
            ResponseFormat = typeof(DiseaseAnalysisPayload)
        };
        var response = await _chatCompletion.GetChatMessageContentAsync(
            history,
            settings,
            cancellationToken: cancellationToken);
        var payload = JsonSerializer.Deserialize<DiseaseAnalysisPayload>(
            response.Content ?? string.Empty,
            new JsonSerializerOptions(JsonSerializerDefaults.Web))
            ?? throw new InvalidDataException("Disease model returned no structured result.");

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
