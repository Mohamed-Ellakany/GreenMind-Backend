using System.Text.Json;
using System.Text.Json.Serialization;
using Greenmind.Application.Dtos.TaskProposals;
using Greenmind.Application.Interfaces.AI;
using Greenmind.Domain.Consts.Enums;
using Greenmind.Infrastructure.Iti;

namespace Greenmind.Infrastructure.TaskProposals;

public sealed class PlanTaskProposalGenerator : IPlanTaskProposalGenerator
{
    private readonly IItiBedrockGatewayClient _gatewayClient;

    public PlanTaskProposalGenerator(IItiBedrockGatewayClient gatewayClient) =>
        _gatewayClient = gatewayClient;

    public async Task<IReadOnlyList<GeneratedTaskChangeItem>> TryGenerateAsync(
        string userRequest,
        string planContextJson,
        CancellationToken cancellationToken = default)
    {
        var systemPrompt =
            """
            أنت محلل طلبات تشغيلية لخطة تصدير زراعية. لا تنفذ أي تغيير.
            أنشئ اقتراحًا فقط إذا طلب المستخدم بوضوح إضافة مهمة أو تعديل مهمة أو إلغاء مهمة.
            الأسئلة والاستفسارات والنصائح العامة يجب أن تعيد hasProposal=false.
            لا تقترح تعديل أو إلغاء مهمة منتهية أو جارية أو ملغاة أو في الماضي.
            استخدم معرّفات المهام والأنواع والتواريخ الموجودة في سياق الخطة.
            operation يجب أن تكون add أو modify أو cancel.
            في cancel كرر القيم الحالية داخل proposedValues.
            reason يجب أن يكون عربيًا وموجزًا. أعد JSON فقط.
            """;
        var userPrompt = $"""
            طلب المستخدم:
            {userRequest}

            سياق الخطة:
            {planContextJson}
            """;

        var response = await _gatewayClient.ChatAsync(
            systemPrompt,
            [new ItiChatMessage("user", userPrompt)],
            modelId: ItiBedrockGatewayClient.DefaultStructuredModel,
            cancellationToken: cancellationToken);
        var payload = JsonSerializer.Deserialize<ProposalPayload>(
            ItiJson.ExtractObject(response),
            new JsonSerializerOptions(JsonSerializerDefaults.Web));
        if (payload is null || !payload.HasProposal || payload.Items.Count == 0)
            return [];

        return payload.Items
            .Take(20)
            .Select(item => new GeneratedTaskChangeItem(
                item.Operation,
                item.FarmTaskId,
                new ProposedTaskValuesDto(
                    item.ProposedValues.TaskTypeId,
                    item.ProposedValues.Title,
                    item.ProposedValues.Description,
                    item.ProposedValues.PlannedDate,
                    ParsePriority(item.ProposedValues.Priority)),
                item.Reason))
            .ToList();
    }

    private static TaskPriority ParsePriority(string value) =>
        value.Trim().ToLowerInvariant() switch
        {
            "low" => TaskPriority.Low,
            "medium" => TaskPriority.Medium,
            "high" => TaskPriority.High,
            "critical" => TaskPriority.Critical,
            _ => throw new InvalidDataException("Invalid generated task priority.")
        };

    private sealed record ProposalPayload
    {
        [JsonPropertyName("hasProposal")]
        public required bool HasProposal { get; init; }

        [JsonPropertyName("items")]
        public required List<ProposalItemPayload> Items { get; init; }
    }

    private sealed record ProposalItemPayload
    {
        [JsonPropertyName("operation")]
        public required string Operation { get; init; }

        [JsonPropertyName("farmTaskId")]
        public int? FarmTaskId { get; init; }

        [JsonPropertyName("proposedValues")]
        public required ProposedValuesPayload ProposedValues { get; init; }

        [JsonPropertyName("reason")]
        public required string Reason { get; init; }
    }

    private sealed record ProposedValuesPayload
    {
        [JsonPropertyName("taskTypeId")]
        public required int TaskTypeId { get; init; }

        [JsonPropertyName("title")]
        public required string Title { get; init; }

        [JsonPropertyName("description")]
        public string? Description { get; init; }

        [JsonPropertyName("plannedDate")]
        public required DateOnly PlannedDate { get; init; }

        [JsonPropertyName("priority")]
        public required string Priority { get; init; }
    }
}
