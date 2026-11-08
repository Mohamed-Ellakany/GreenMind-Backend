using System.Text.Json;
using Greenmind.Application.Contracts.ExportPlans;
using Greenmind.Application.Contracts.Persistence;
using Greenmind.Application.Dtos.ExportPlans;
using Greenmind.Domain.Entities;
using Greenmind.Infrastructure.ExportPlans;
using Greenmind.Infrastructure.Iti;
using Xunit;

namespace Greenmind.Application.Tests.ExportPlans;

public sealed class TrustedExportScheduleAdvisorTests
{
    [Fact]
    public async Task AnalyzeAsync_WhenInferredFactValueIsNumber_DeserializesSuccessfullyWithoutException()
    {
        // This reproduces the exact error reported:
        // System.InvalidOperationException: Cannot get the value of a token type 'Number' as a string. Path: $.inferredFacts[0].value
        const string aiJsonResponse = """
        {
          "seasonName": "الموسم الشتوي",
          "plantingDate": "2026-09-01",
          "expectedHarvestDate": "2027-02-15",
          "expectedExportDate": "2027-03-01",
          "assessment": "Feasible",
          "confidence": "High",
          "summary": "موعد الزراعة المقترح متوافق مع زراعة الفراولة في منطقة دمياط.",
          "reasons": ["مناخ مناسب لزراعة الفراولة"],
          "warnings": [],
          "recommendations": ["الحرص على الري بالتنقيط"],
          "assumptions": [],
          "inferredFacts": [
            {
              "field": "cropMaturityDays",
              "value": 165,
              "reason": "فترة النضج المعتادة للفراولة 165 يوماً"
            },
            {
              "field": "targetTemperature",
              "value": 22.5,
              "reason": "درجة الحرارة المثالية"
            },
            {
              "field": "isGreenhouse",
              "value": true,
              "reason": "الزراعة مكشوفة"
            }
          ]
        }
        """;

        var gatewayStub = new GatewayStub(aiJsonResponse);
        var climateStub = new ClimateDataClientStub();
        var knowledgeStub = new KnowledgeRepositoryStub();
        var advisor = new TrustedExportScheduleAdvisor(
            climateStub,
            knowledgeStub,
            gatewayStub,
            TimeProvider.System);

        var context = new ExportScheduleAdvisorContext(
            Crop: "Strawberry",
            CropDescription: "Fresh strawberry crop",
            Destination: "European Union",
            DestinationDescription: "EU standards",
            GovernorateDisplayName: "دمياط",
            CityDisplayName: "مركز الزرقا",
            Latitude: 31.2090,
            Longitude: 31.6490,
            AreaInFeddan: 5,
            Notes: null,
            Today: new DateOnly(2026, 8, 17),
            RequestedPlantingDate: null,
            RequestedHarvestDate: null,
            RequestedExportDate: null,
            HistoricalSchedules: [],
            OwnerUserId: "test-user",
            FarmId: 1);

        var result = await advisor.AnalyzeAsync(context);

        Assert.NotNull(result);
        Assert.Equal("الموسم الشتوي", result.SeasonName);
        Assert.Equal(new DateOnly(2026, 9, 1), result.PlantingDate);
        Assert.Equal(new DateOnly(2027, 2, 15), result.ExpectedHarvestDate);
        Assert.Equal(new DateOnly(2027, 3, 1), result.ExpectedExportDate);
        Assert.Contains(result.InferredFacts, f => f.Field == "cropMaturityDays" && f.Value == "165");
        Assert.Contains(result.InferredFacts, f => f.Field == "targetTemperature" && f.Value == "22.5");
        Assert.Contains(result.InferredFacts, f => f.Field == "isGreenhouse" && f.Value == "true");
        Assert.Contains("الفراولة", gatewayStub.CapturedUserMessage);
    }

    [Fact]
    public async Task AnalyzeAsync_WhenUserProvidesCustomPlantingDate_CalculatesConsistentSequence()
    {
        const string aiJsonResponse = """
        {
          "seasonName": "موسم مخصص",
          "plantingDate": "2026-09-01",
          "expectedHarvestDate": "2027-02-15",
          "expectedExportDate": "2027-03-01",
          "assessment": "Feasible",
          "confidence": "Medium",
          "summary": "تم تقييم الموعد المخصص.",
          "reasons": [],
          "warnings": [],
          "recommendations": [],
          "assumptions": [],
          "inferredFacts": []
        }
        """;

        var requestedPlanting = new DateOnly(2026, 10, 15);
        var gatewayStub = new GatewayStub(aiJsonResponse);
        var climateStub = new ClimateDataClientStub();
        var knowledgeStub = new KnowledgeRepositoryStub();
        var advisor = new TrustedExportScheduleAdvisor(
            climateStub,
            knowledgeStub,
            gatewayStub,
            TimeProvider.System);

        var context = new ExportScheduleAdvisorContext(
            Crop: "Strawberry",
            CropDescription: "Fresh strawberry crop",
            Destination: "European Union",
            DestinationDescription: "EU standards",
            GovernorateDisplayName: "دمياط",
            CityDisplayName: "مركز الزرقا",
            Latitude: 31.2090,
            Longitude: 31.6490,
            AreaInFeddan: 5,
            Notes: null,
            Today: new DateOnly(2026, 8, 17),
            RequestedPlantingDate: requestedPlanting,
            RequestedHarvestDate: null,
            RequestedExportDate: null,
            HistoricalSchedules: [],
            OwnerUserId: "test-user",
            FarmId: 1);

        var result = await advisor.AnalyzeAsync(context);

        Assert.NotNull(result);
        Assert.Equal(requestedPlanting, result.PlantingDate);
        Assert.True(result.ExpectedHarvestDate > result.PlantingDate);
        Assert.True(result.ExpectedExportDate >= result.ExpectedHarvestDate);
    }

    private sealed class GatewayStub(string response) : IItiBedrockGatewayClient
    {
        public string CapturedUserMessage { get; private set; } = string.Empty;

        public Task<string> ChatAsync(
            string? systemPrompt,
            IReadOnlyList<ItiChatMessage> messages,
            string? modelId = null,
            CancellationToken cancellationToken = default,
            ItiAiRequestContext? usageContext = null)
        {
            CapturedUserMessage = messages.FirstOrDefault()?.Content ?? string.Empty;
            return Task.FromResult(response);
        }

        public Task<string> MultimodalChatAsync(
            string? systemPrompt,
            string text,
            IReadOnlyList<ItiImageInput> images,
            int maxTokens = 1200,
            string? modelId = null,
            CancellationToken cancellationToken = default,
            ItiAiRequestContext? usageContext = null) =>
            throw new NotImplementedException();
    }

    private sealed class ClimateDataClientStub : IClimateDataClient
    {
        public Task<ClimateDataSnapshot> GetClimateAsync(
            double latitude,
            double longitude,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new ClimateDataSnapshot(
                latitude,
                longitude,
                [],
                DateTime.UtcNow,
                "NASA POWER",
                "https://power.larc.nasa.gov"));
        }
    }

    private sealed class KnowledgeRepositoryStub : IComplianceDocumentRepository
    {
        public Task<List<ComplianceSearchResult>> SearchTextAsync(
            string searchText,
            Greenmind.Domain.Consts.Enums.ComplianceCategory? category = null,
            string? crop = null,
            string? destination = null,
            int topK = 5,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(new List<ComplianceSearchResult>());

        public IQueryable<ComplianceDocument> Query() => Enumerable.Empty<ComplianceDocument>().AsQueryable();
        public Task<IReadOnlyList<ComplianceDocument>> GetAllAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<ComplianceDocument>>([]);
        public Task<ComplianceDocument?> GetByIdAsync(int id, CancellationToken cancellationToken = default) =>
            Task.FromResult<ComplianceDocument?>(null);
        public Task<IReadOnlyList<ComplianceDocument>> FindAsync(System.Linq.Expressions.Expression<Func<ComplianceDocument, bool>> predicate, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<ComplianceDocument>>([]);
        public Task<ComplianceDocument?> FirstOrDefaultAsync(System.Linq.Expressions.Expression<Func<ComplianceDocument, bool>> predicate, CancellationToken cancellationToken = default, params System.Linq.Expressions.Expression<Func<ComplianceDocument, object>>[] includes) =>
            Task.FromResult<ComplianceDocument?>(null);
        public Task<bool> AnyAsync(System.Linq.Expressions.Expression<Func<ComplianceDocument, bool>> predicate, CancellationToken cancellationToken = default) =>
            Task.FromResult(false);
        public Task AddAsync(ComplianceDocument entity, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
        public Task AddRangeAsync(IEnumerable<ComplianceDocument> entities, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
        public void Update(ComplianceDocument entity) { }
        public void Remove(ComplianceDocument entity) { }
        public void RemoveRange(IEnumerable<ComplianceDocument> entities) { }
    }
}
