using Greenmind.Application.Contracts.Regulatory;
using Greenmind.Application.Dtos.Regulatory;
using Greenmind.Domain.Consts.Enums;
using Greenmind.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Greenmind.Infrastructure.Regulatory;

public sealed class RegulatoryKnowledgeQueryService(GreenmindDbContext context)
    : IRegulatoryKnowledgeQueryService
{
    private const int MaximumResults = 10;

    public async Task<IReadOnlyList<ActiveSubstanceKnowledgeDto>> FindActiveSubstancesAsync(
        string regionName,
        string nameOrCasOrId,
        CancellationToken cancellationToken = default)
    {
        var region = NormalizeRequired(regionName);
        var search = ResolveActiveSubstanceSearch(NormalizeRequired(nameOrCasOrId));
        var isExternalId = int.TryParse(search, out var externalId);

        var items = await context.ActiveSubstances
            .AsNoTracking()
            .Where(item => !item.IsDeleted
                && item.RegionName == region
                && ((isExternalId && item.ExternalId == externalId)
                    || item.CasNumber == search
                    || item.Name.Contains(search)))
            .OrderByDescending(item => item.Name == search || item.CasNumber == search || item.ExternalId == externalId)
            .ThenBy(item => item.Name)
            .Take(MaximumResults)
            .ToListAsync(cancellationToken);

        return items
            .Select(item => new ActiveSubstanceKnowledgeDto(
                item.ExternalId,
                item.RegionName,
                item.Name,
                item.CasNumber,
                ToArabicStatus(item.ApprovalStatus),
                item.ApprovalExpiresOn,
                item.Legislation,
                item.Remark,
                item.RegulationReference,
                item.Functions))
            .ToList();
    }

    public async Task<IReadOnlyList<ProductMrlKnowledgeDto>> FindProductMrlsAsync(
        string regionName,
        string productType,
        string residueNameOrPesticideId,
        CancellationToken cancellationToken = default)
    {
        var region = NormalizeRequired(regionName);
        var product = NormalizeRequired(productType);
        var search = NormalizeRequired(residueNameOrPesticideId);

        var items = await context.ProductMrls
            .AsNoTracking()
            .Where(item => !item.IsDeleted
                && item.RegionName == region
                && item.ProductType == product
                && (item.ExternalPesticideId == search || item.ResidueName.Contains(search)))
            .OrderByDescending(item => item.ExternalPesticideId == search || item.ResidueName == search)
            .ThenBy(item => item.ResidueName)
            .Take(MaximumResults)
            .ToListAsync(cancellationToken);

        return items
            .Select(item => new ProductMrlKnowledgeDto(
                item.RegionName,
                item.ProductType,
                item.ExternalPesticideId,
                item.ResidueName,
                item.MaximumResidueLevel,
                item.IsDetectionLimit,
                item.Unit))
            .ToList();
    }

    private static string NormalizeRequired(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("Search values cannot be empty.", nameof(value));
        }

        return value.Trim();
    }

    private static string ResolveActiveSubstanceSearch(string search) =>
        search switch
        {
            // The regulatory workbook stores substance names in English, while farm
            // owners commonly ask for them in Arabic. Keep aliases deterministic so
            // a translated display name cannot turn an existing row into "not found".
            "بروبيكونازول" => "Propiconazole",
            "أبامكتين" => "Abamectin",
            "ابامكتين" => "Abamectin",
            "إيميداكلوبريد" => "Imidacloprid",
            "ايميداكلوبريد" => "Imidacloprid",
            _ => search
        };

    private static string ToArabicStatus(ActiveSubstanceApprovalStatus status) => status switch
    {
        ActiveSubstanceApprovalStatus.قيد_المراجعة => "قيد المراجعة",
        ActiveSubstanceApprovalStatus.غير_معتمد => "غير معتمد",
        ActiveSubstanceApprovalStatus.معتمد => "معتمد",
        _ => "غير معروف"
    };
}
