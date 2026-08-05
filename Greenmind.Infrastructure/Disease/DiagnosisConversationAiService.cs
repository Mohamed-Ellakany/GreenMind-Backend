using System.Text.Json;
using System.Text.Json.Serialization;
using Greenmind.Application.Dtos.Disease;
using Greenmind.Application.Interfaces.AI;
using Greenmind.Infrastructure.Iti;

namespace Greenmind.Infrastructure.Disease;

public sealed class DiagnosisConversationAiService(
    IItiBedrockGatewayClient gatewayClient) : IDiagnosisConversationAiService
{
    public async Task<DiagnosisConversationTurn> ReplyAsync(
        DiagnosisConversationRequest request,
        CancellationToken cancellationToken = default)
    {
        const string systemPrompt = """
            أنت مساعد زراعي مصري يجمع معلومات لتشخيص أمراض الفراولة. هذه مرحلة جمع معلومات فقط:
            لا تصدر تشخيصًا نهائيًا، ولا تقترح مبيدًا أو جرعة، ولا تدّعِ فحصًا معمليًا.
            لا تسأل أي أسئلة داخل assistantText؛ الخادم سيضيف السؤال التالي بعد حفظ الحقائق.
            اجعل assistantText إقرارًا قصيرًا بما استلمته، ووصفًا مختصرًا لما يظهر بالصورة إن وُجدت.
            استخرج الحقائق المؤكدة فقط من كلام المستخدم. أبقِ القيمة السابقة إذا لم يصححها المستخدم.
            الرسائل السابقة مرتبة زمنيًا وهي جزء موثوق من سياق الجلسة؛ راجعها مع الحقائق الحالية قبل الرد.
            افهم الإجابات المختصرة باعتبارها ردًا على آخر سؤال طرحته، ولا تكرر سؤالًا سبق أن أجاب عنه المستخدم.
            استخرج الحقائق من الرسالة الحالية والسياق السابق حتى لو كانت الإجابة مختصرة أو عامية.
            أعد JSON فقط بالمخطط التالي:
            {
              "assistantText":"رد عربي مصري مهني",
              "facts":{
                "affectedPart":null,"onset":null,"spreadPattern":null,
                "affectedExtent":null,"conditions":null,"previousTreatments":null
              }
            }
            """;

        var prompt = JsonSerializer.Serialize(new
        {
            planContext = JsonDocument.Parse(request.PlanContextJson).RootElement,
            currentFacts = request.CurrentFacts,
            totalImageCount = request.TotalImageCount,
            currentMessage = request.CurrentMessage,
            recentMessages = request.RecentMessages.Select(item => new
            {
                role = item.Role.ToString(),
                item.Content
            })
        });

        var response = request.CurrentImages.Count > 0
            ? await gatewayClient.MultimodalChatAsync(
                systemPrompt,
                prompt,
                request.CurrentImages.Select(image =>
                    new ItiImageInput(image.Format, image.DataBase64)).ToList(),
                modelId: ItiBedrockGatewayClient.DefaultVisionModel,
                cancellationToken: cancellationToken)
            : await gatewayClient.ChatAsync(
                systemPrompt,
                [new ItiChatMessage("user", prompt)],
                modelId: ItiBedrockGatewayClient.DefaultStructuredModel,
                cancellationToken: cancellationToken);

        TurnPayload payload;
        try
        {
            payload = ParsePayload(response);
        }
        catch (Exception exception) when (exception is JsonException or InvalidDataException)
        {
            // Repair malformed structured output without resending the user's image.
            var repairPrompt = JsonSerializer.Serialize(new
            {
                instruction = "Return valid JSON only using the required diagnosis conversation schema.",
                originalRequest = prompt,
                invalidResponse = response
            });
            var repaired = await gatewayClient.ChatAsync(
                systemPrompt,
                [new ItiChatMessage("user", repairPrompt)],
                modelId: ItiBedrockGatewayClient.DefaultStructuredModel,
                cancellationToken: cancellationToken);
            payload = ParsePayload(repaired);
        }

        if (string.IsNullOrWhiteSpace(payload.AssistantText))
            throw new InvalidDataException("Diagnosis conversation returned an empty reply.");

        var facts = payload.Facts ?? new FactsPayload();
        return new DiagnosisConversationTurn(
            payload.AssistantText.Trim(),
            new DiagnosisFactsDto(
                Choose(facts.AffectedPart, request.CurrentFacts.AffectedPart),
                Choose(facts.Onset, request.CurrentFacts.Onset),
                Choose(facts.SpreadPattern, request.CurrentFacts.SpreadPattern),
                Choose(facts.AffectedExtent, request.CurrentFacts.AffectedExtent),
                Choose(facts.Conditions, request.CurrentFacts.Conditions),
                Choose(facts.PreviousTreatments, request.CurrentFacts.PreviousTreatments)));
    }

    private static string? Choose(string? current, string? previous) =>
        string.IsNullOrWhiteSpace(current) ? previous : current.Trim();

    private static TurnPayload ParsePayload(string response) =>
        JsonSerializer.Deserialize<TurnPayload>(
            ItiJson.ExtractObject(response),
            new JsonSerializerOptions(JsonSerializerDefaults.Web))
        ?? throw new InvalidDataException("Diagnosis conversation returned no structured response.");

    private sealed record TurnPayload
    {
        [JsonPropertyName("assistantText")]
        public string AssistantText { get; init; } = string.Empty;

        [JsonPropertyName("facts")]
        public FactsPayload? Facts { get; init; }
    }

    private sealed record FactsPayload
    {
        public string? AffectedPart { get; init; }
        public string? Onset { get; init; }
        public string? SpreadPattern { get; init; }
        public string? AffectedExtent { get; init; }
        public string? Conditions { get; init; }
        public string? PreviousTreatments { get; init; }
    }
}
