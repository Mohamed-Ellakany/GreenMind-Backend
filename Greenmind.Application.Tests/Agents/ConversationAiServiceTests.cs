using Greenmind.Application.Contracts.Regulatory;
using Greenmind.Application.Dtos.Regulatory;
using Greenmind.Application.Interfaces.AI;
using Greenmind.Domain.Consts.Enums;
using Greenmind.Infrastructure.Agents;
using Greenmind.Infrastructure.Iti;

namespace Greenmind.Application.Tests.Agents;

public sealed class ConversationAiServiceTests
{
    [Fact]
    public void RequiresRegulatoryLookup_ForArabicPesticideQuestion()
    {
        var request = BuildRequest(
            "بيكول 25 EC - المادة الفعالة بروبيكونازول 25%، ينفع أستخدم المبيد ده؟");

        Assert.True(ConversationAiService.RequiresRegulatoryLookup(request));
    }

    [Fact]
    public void RequiresRegulatoryLookup_ForShortPlanFollowUpToPesticideQuestion()
    {
        var request = BuildRequest(
            "في الخطة الثانية",
            new ConversationHistoryItem(
                ChatMessageRole.User,
                "هل مادة بروبيكونازول في المبيد معتمدة للتصدير؟"));

        Assert.True(ConversationAiService.RequiresRegulatoryLookup(request));
    }

    [Fact]
    public void DoesNotRequireRegulatoryLookup_ForUnrelatedFarmQuestion()
    {
        var request = BuildRequest("لخص لي المهام المتأخرة اليوم");

        Assert.False(ConversationAiService.RequiresRegulatoryLookup(request));
    }

    [Fact]
    public async Task PesticideDecision_UsesExistingNonApprovedRecordWithoutCallingModel()
    {
        var request = new ConversationAiRequest(
            ChatScope.ExportPlan,
            "اختبار",
            "المادة الفعالة بروبيكونازول 25%، ينفع أستخدم المبيد ده؟",
            """{"plan":{"crop":"Strawberry","destination":"European Union"}}""",
            null,
            []);
        var service = new ConversationAiService(
            new ModelMustNotBeCalled(),
            new RegulatoryKnowledgeStub());
        var chunks = new List<string>();

        await foreach (var chunk in service.StreamReplyAsync(request))
            chunks.Add(chunk);

        var reply = string.Concat(chunks);
        Assert.Contains("موجودة", reply);
        Assert.Contains("غير معتمد", reply);
        Assert.Contains("60207-90-1", reply);
        Assert.DoesNotContain("غير مدرجة", reply);
        Assert.DoesNotContain("غير موجودة", reply);
    }

    [Fact]
    public async Task PesticideDecision_UsesActiveSubstancesFromCurrentQuestion()
    {
        var request = new ConversationAiRequest(
            ChatScope.ExportPlan,
            "اختبار",
            "هل ممكن استخدم مبيد يحتوي على Abamectin 2.8% و Imidacloprid 30%؟",
            """{"plan":{"crop":"Strawberry","destination":"European Union"}}""",
            null,
            []);
        var service = new ConversationAiService(
            new ModelMustNotBeCalled(),
            new RegulatoryKnowledgeStub());
        var chunks = new List<string>();

        await foreach (var chunk in service.StreamReplyAsync(request))
            chunks.Add(chunk);

        var reply = string.Concat(chunks);
        Assert.Contains("Abamectin", reply);
        Assert.Contains("Imidacloprid", reply);
        Assert.Contains("غير معتمد", reply);
        Assert.DoesNotContain("Propiconazole", reply);
        Assert.DoesNotContain("60207-90-1", reply);
    }

    private static ConversationAiRequest BuildRequest(
        string userMessage,
        params ConversationHistoryItem[] recentMessages) =>
        new(
            ChatScope.Farm,
            "اختبار",
            userMessage,
            "{}",
            null,
            recentMessages);

    private sealed class RegulatoryKnowledgeStub : IRegulatoryKnowledgeQueryService
    {
        public Task<IReadOnlyList<ActiveSubstanceKnowledgeDto>> FindActiveSubstancesAsync(
            string regionName,
            string nameOrCasOrId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<ActiveSubstanceKnowledgeDto>>(
                nameOrCasOrId switch
                {
                    "Propiconazole" =>
                    [
                        new(
                            75,
                            regionName,
                            "Propiconazole",
                            "60207-90-1",
                            "غير معتمد",
                            null,
                            null,
                            null,
                            null,
                            null)
                    ],
                    "Abamectin" =>
                    [
                        new(
                            1,
                            regionName,
                            "Abamectin",
                            "71751-41-2",
                            "معتمد",
                            null,
                            null,
                            null,
                            null,
                            null)
                    ],
                    "Imidacloprid" =>
                    [
                        new(
                            2,
                            regionName,
                            "Imidacloprid",
                            "138261-41-3",
                            "غير معتمد",
                            null,
                            null,
                            null,
                            null,
                            null)
                    ],
                    _ => []
                });

        public Task<IReadOnlyList<ProductMrlKnowledgeDto>> FindProductMrlsAsync(
            string regionName,
            string productType,
            string residueNameOrPesticideId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<ProductMrlKnowledgeDto>>(
                residueNameOrPesticideId == "Abamectin"
                    ? [new(regionName, productType, "1", "Abamectin", 0.01m, false, "mg/kg")]
                    : []);
    }

    private sealed class ModelMustNotBeCalled : IItiBedrockGatewayClient
    {
        public Task<string> ChatAsync(
            string? systemPrompt,
            IReadOnlyList<ItiChatMessage> messages,
            string? modelId = null,
            CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("The deterministic regulatory path must not call the model.");

        public Task<string> MultimodalChatAsync(
            string? systemPrompt,
            string text,
            IReadOnlyList<ItiImageInput> images,
            int maxTokens = 1200,
            string? modelId = null,
            CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("The deterministic regulatory path must not call the model.");

    }
}
