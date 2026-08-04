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

        return items.Select(MapActiveSubstance).ToList();
    }

    public async Task<IReadOnlyList<ActiveSubstanceKnowledgeDto>> FindActiveSubstancesMentionedAsync(
        string regionName,
        string text,
        CancellationToken cancellationToken = default)
    {
        var region = NormalizeRequired(regionName);
        var sourceText = NormalizeRequired(text);

        // The user's sentence cannot be translated into a reliable SQL "contains row name"
        // expression across all supported providers. The EU workbook is small, so load only
        // the read-only rows for this region and match complete substance names/CAS values in
        // memory. This keeps the database itself as the source of truth instead of maintaining
        // a hard-coded allow-list in the assistant.
        var items = await context.ActiveSubstances
            .AsNoTracking()
            .Where(item => !item.IsDeleted && item.RegionName == region)
            .ToListAsync(cancellationToken);

        return items
            .Where(item => ContainsCompleteValue(sourceText, item.Name) ||
                (!string.IsNullOrWhiteSpace(item.CasNumber) &&
                    ContainsCompleteValue(sourceText, item.CasNumber)))
            .OrderByDescending(item => item.Name.Length)
            .ThenBy(item => item.Name)
            .Take(MaximumResults)
            .Select(MapActiveSubstance)
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

    private static ActiveSubstanceKnowledgeDto MapActiveSubstance(
        Domain.Entities.ActiveSubstance item) =>
        new(
            item.ExternalId,
            item.RegionName,
            item.Name,
            item.CasNumber,
            ToArabicStatus(item.ApprovalStatus),
            item.ApprovalExpiresOn,
            item.Legislation,
            item.Remark,
            item.RegulationReference,
            item.Functions);

    private static bool ContainsCompleteValue(string source, string candidate)
    {
        if (string.IsNullOrWhiteSpace(candidate))
            return false;

        var searchFrom = 0;
        while (searchFrom < source.Length)
        {
            var index = source.IndexOf(candidate, searchFrom, StringComparison.OrdinalIgnoreCase);
            if (index < 0)
                return false;

            var end = index + candidate.Length;
            var hasValidStart = index == 0 || !IsIdentifierCharacter(source[index - 1]);
            var hasValidEnd = end == source.Length || !IsIdentifierCharacter(source[end]);
            if (hasValidStart && hasValidEnd)
                return true;

            searchFrom = index + 1;
        }

        return false;
    }

    private static bool IsIdentifierCharacter(char value) =>
        char.IsLetterOrDigit(value) || value is '_' or '-';

    private static string ToArabicStatus(ActiveSubstanceApprovalStatus status) => status switch
    {
        ActiveSubstanceApprovalStatus.قيد_المراجعة => "قيد المراجعة",
        ActiveSubstanceApprovalStatus.غير_معتمد => "غير معتمد",
        ActiveSubstanceApprovalStatus.معتمد => "معتمد",
        _ => "غير معروف"
    };
}
