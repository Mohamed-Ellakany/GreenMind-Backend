using System.Text.Json;
using Greenmind.Application.Dtos.Disease;
using Greenmind.Application.Interfaces.AI;
using Greenmind.Infrastructure.Disease;
using Greenmind.Infrastructure.Iti;
using Xunit;

namespace Greenmind.Application.Tests.Disease;

public sealed class DiagnosisConversationAiServiceTests
{
    [Fact]
    public async Task ReplyAsync_WhenModelReturnsPlainArabicText_FallsBackGracefullyWithoutCrashing()
    {
        var gateway = new GatewayStub(
            multimodalResponse: "بناءً على الصورة المرفقة، يظهر على ثمار الفراولة بقع بنية تدل على إصابة فطرية بالعفن الرمادي.",
            chatResponse: "رد نصي بدون كائن json");

        var service = new DiagnosisConversationAiService(gateway);

        var request = new DiagnosisConversationRequest(
            "{}",
            "",
            new DiagnosisFactsDto(null, null, null, null, null, null),
            [],
            [new ConversationImage("jpeg", "dGVzdA==")],
            1,
            "user-123",
            1);

        var turn = await service.ReplyAsync(request);

        Assert.NotNull(turn);
        Assert.Contains("العفن الرمادي", turn.AssistantText);
    }

    [Fact]
    public void ItiJson_StripThinkTags_RemovesReasoningPrefixCleanly()
    {
        var raw = "<think>Analyzing strawberry photo for botrytis cinerea...</think>{\"assistantText\":\"ثمار الفراولة مصابة\",\"facts\":null}";
        var extracted = ItiJson.ExtractObject(raw);

        using var doc = JsonDocument.Parse(extracted);
        Assert.Equal("ثمار الفراولة مصابة", doc.RootElement.GetProperty("assistantText").GetString());
    }

    private sealed class GatewayStub(string multimodalResponse, string chatResponse) : IItiBedrockGatewayClient
    {
        public Task<string> ChatAsync(
            string? systemPrompt,
            IReadOnlyList<ItiChatMessage> messages,
            string? modelId = null,
            CancellationToken cancellationToken = default,
            ItiAiRequestContext? usageContext = null) =>
            Task.FromResult(chatResponse);

        public Task<string> MultimodalChatAsync(
            string? systemPrompt,
            string text,
            IReadOnlyList<ItiImageInput> images,
            int maxTokens = 1200,
            string? modelId = null,
            CancellationToken cancellationToken = default,
            ItiAiRequestContext? usageContext = null) =>
            Task.FromResult(multimodalResponse);
    }
}
