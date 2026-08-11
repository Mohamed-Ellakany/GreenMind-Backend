using Greenmind.Infrastructure.Iti;
using Greenmind.Infrastructure.TaskProposals;

namespace Greenmind.Application.Tests.TaskProposals;

public sealed class PlanTaskProposalGeneratorTests
{
    [Theory]
    [InlineData("{\"hasProposal\":false}")]
    [InlineData("{\"hasProposal\":true}")]
    [InlineData("not JSON")]
    public async Task TryGenerateAsync_ReturnsNoItems_WhenAiResponseIsIncomplete(string response)
    {
        var generator = new PlanTaskProposalGenerator(new GatewayStub(response));

        var result = await generator.TryGenerateAsync("Create treatment tasks", "{}");

        Assert.Empty(result);
    }

    [Fact]
    public async Task TryGenerateAsync_ReturnsNoItems_WhenUserFacingTaskTextIsNotArabic()
    {
        var generator = new PlanTaskProposalGenerator(new GatewayStub(
            """
            {"hasProposal":true,"items":[{"operation":"add","farmTaskId":null,"proposedValues":{"taskTypeId":8,"title":"Follow up infection","description":"Inspect affected plants","quantity":1,"unit":"visit","plannedDate":"2026-08-06","priority":"high"},"reason":"Follow up"}]}
            """));

        var result = await generator.TryGenerateAsync("Create treatment tasks", "{}");

        Assert.Empty(result);
    }

    private sealed class GatewayStub(string response) : IItiBedrockGatewayClient
    {
        public Task<string> ChatAsync(
            string? systemPrompt,
            IReadOnlyList<ItiChatMessage> messages,
            string? modelId = null,
            CancellationToken cancellationToken = default,
            ItiAiRequestContext? usageContext = null) => Task.FromResult(response);

        public Task<string> MultimodalChatAsync(
            string? systemPrompt,
            string text,
            IReadOnlyList<ItiImageInput> images,
            int maxTokens = 1200,
            string? modelId = null,
            CancellationToken cancellationToken = default,
            ItiAiRequestContext? usageContext = null) => throw new NotSupportedException();
    }
}
