using Greenmind.Domain.Consts;
using Greenmind.Domain.Consts.Enums;
using Greenmind.Domain.Entities;
using Greenmind.Infrastructure.Agents.Plugins;
using Greenmind.Infrastructure.Persistence;
using Greenmind.Infrastructure.Persistence.Repositories;
using Microsoft.EntityFrameworkCore;

namespace Greenmind.Application.Tests.Rag;

public sealed class ComplianceRagCanonicalCropTests
{
    [Theory]
    [InlineData(CropCatalog.Strawberry.DefaultDisplayName)]
    [InlineData("Renamed premium berries")]
    public async Task CanonicalTreatmentDocument_IsFound_FromLegacyOrCurrentDisplayName(string cropFilter)
    {
        var options = new DbContextOptionsBuilder<GreenmindDbContext>()
            .UseInMemoryDatabase($"rag-crop-key-{Guid.NewGuid():N}")
            .Options;
        await using var context = new GreenmindDbContext(options);
        context.Crops.Add(new Crop
        {
            Id = CropCatalog.Strawberry.Id,
            Name = "Renamed premium berries"
        });
        context.ComplianceDocuments.Add(new ComplianceDocument
        {
            Category = ComplianceCategory.DiseaseTreatment,
            Crop = CropCatalog.Strawberry.CanonicalKey,
            SourceName = "Canonical strawberry treatment source",
            Content = "Botrytis treatment guidance from the canonical crop cache.",
            Embedding = "[]"
        });
        await context.SaveChangesAsync();

        var repository = new ComplianceDocumentRepository(context);
        var plugin = new ComplianceRagPlugin(repository);

        var result = await plugin.SearchDiseaseTreatmentAsync("Botrytis treatment", cropFilter);

        Assert.Contains("canonical crop cache", result, StringComparison.OrdinalIgnoreCase);
    }
}
