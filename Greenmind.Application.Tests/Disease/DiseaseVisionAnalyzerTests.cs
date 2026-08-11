using Greenmind.Application.Dtos.Disease;
using Greenmind.Domain.Consts.Enums;
using Greenmind.Infrastructure.Disease;
using Greenmind.Infrastructure.Iti;
using Microsoft.Extensions.Configuration;

namespace Greenmind.Application.Tests.Disease;

public sealed class DiseaseVisionAnalyzerTests
{
    private const string ValidResponse =
        """
        {
          "suspectedDisease": "Leaf spot",
          "confidencePercentage": 82.5,
          "severity": "medium",
          "observedSymptoms": "Brown spots",
          "recommendedActions": "Inspect the crop",
          "exportImpact": "Review affected produce",
          "advisoryWarning": "Consult a specialist"
        }
        """;

    [Fact]
    public async Task AnalyzeAsync_RequestsExactSchemaAndParsesCompleteResponse()
    {
        var gateway = new GatewayStub(ValidResponse);
        var analyzer = CreateAnalyzer(gateway);

        var result = await analyzer.AnalyzeAsync(CreateRequest());

        Assert.Equal("Leaf spot", result.SuspectedDisease);
        Assert.Equal(82.5m, result.ConfidencePercentage);
        Assert.Equal(DiseaseSeverity.Medium, result.Severity);
        Assert.Single(gateway.Prompts);
        Assert.Contains("\"suspectedDisease\"", gateway.Prompts[0]);
        Assert.Contains("Never rename or omit a property", gateway.Prompts[0]);
    }

    [Fact]
    public async Task AnalyzeAsync_RetriesOnceWhenRequiredPropertiesAreMissing()
    {
        var gateway = new GatewayStub(
            """{"recommendedActions":"Inspect","exportImpact":"Unknown","advisoryWarning":"Consult"}""",
            ValidResponse);
        var analyzer = CreateAnalyzer(gateway);

        var result = await analyzer.AnalyzeAsync(CreateRequest());

        Assert.Equal("Leaf spot", result.SuspectedDisease);
        Assert.Equal(2, gateway.Prompts.Count);
        Assert.Contains("complete replacement response", gateway.Prompts[1]);
    }

    [Fact]
    public async Task AnalyzeAsync_ThrowsInvalidDataExceptionAfterInvalidRetry()
    {
        var incompleteResponse = """{"severity":"low"}""";
        var gateway = new GatewayStub(incompleteResponse, incompleteResponse);
        var analyzer = CreateAnalyzer(gateway);

        var exception = await Assert.ThrowsAsync<InvalidDataException>(
            () => analyzer.AnalyzeAsync(CreateRequest()));

        Assert.Equal(2, gateway.Prompts.Count);
        Assert.Contains("after retry", exception.Message);
    }

    private static DiseaseVisionAnalyzer CreateAnalyzer(GatewayStub gateway)
    {
        var configuration = new ConfigurationManager();
        configuration["iti:VisionModel"] = "test-vision-model";
        return new DiseaseVisionAnalyzer(configuration, gateway);
    }

    private static DiseaseVisionRequest CreateRequest() =>
        new(
            "Test plan",
            new DateOnly(2026, 8, 3),
            "Leaves",
            "Test notes",
            [new DiseaseVisionImage("image/jpeg", [0xFF, 0xD8, 0xFF])]);

    private sealed class GatewayStub(params string[] responses) : IItiBedrockGatewayClient
    {
        private readonly Queue<string> _responses = new(responses);

        public List<string> Prompts { get; } = [];

        public Task<string> ChatAsync(
            string? systemPrompt,
            IReadOnlyList<ItiChatMessage> messages,
            string? modelId = null,
            CancellationToken cancellationToken = default,
            ItiAiRequestContext? usageContext = null) =>
            throw new NotSupportedException();

        public Task<string> MultimodalChatAsync(
            string? systemPrompt,
            string text,
            IReadOnlyList<ItiImageInput> images,
            int maxTokens = 1200,
            string? modelId = null,
            CancellationToken cancellationToken = default,
            ItiAiRequestContext? usageContext = null)
        {
            Prompts.Add(text);
            return Task.FromResult(_responses.Dequeue());
        }
    }
}
