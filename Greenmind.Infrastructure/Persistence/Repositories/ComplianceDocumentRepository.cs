using Greenmind.Application.Contracts.Persistence;
using Greenmind.Domain.Consts;
using Greenmind.Domain.Consts.Enums;
using Greenmind.Domain.Entities;
using Greenmind.Infrastructure.Rag;
using Microsoft.EntityFrameworkCore;

namespace Greenmind.Infrastructure.Persistence.Repositories
{
    public class ComplianceDocumentRepository(GreenmindDbContext context)
        : GenericRepository<ComplianceDocument>(context), IComplianceDocumentRepository
    {
        public async Task<List<ComplianceSearchResult>> SearchTextAsync(
            string searchText,
            ComplianceCategory? category = null,
            string? crop = null,
            string? destination = null,
            int topK = 5,
            CancellationToken cancellationToken = default)
        {
            // Pre-filter with SQL before ranking the remaining chunks in memory.
            // Crop/Destination filters also match rows where the stored value is NULL,
            // since NULL means "applies to everything" (e.g. a shared EU-wide regulation).
            IQueryable<ComplianceDocument> query = DbSet.AsNoTracking();

            if (category is not null)
                query = query.Where(d => d.Category == category);

            if (!string.IsNullOrWhiteSpace(crop))
            {
                var cropDisplayName = crop.Trim();
                var cropId = await Context.Crops
                    .AsNoTracking()
                    .Where(item => item.Name == cropDisplayName)
                    .Select(item => (int?)item.Id)
                    .SingleOrDefaultAsync(cancellationToken);
                var stableKey = cropId.HasValue
                    ? CropCatalog.GetCanonicalKey(cropId.Value)
                    : null;
                var cropLookupKeys = CropCatalog.GetLookupKeys(stableKey ?? cropDisplayName)
                    .Append(cropDisplayName)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToArray();
                query = query.Where(d =>
                    d.Crop == null ||
                    (d.Crop != null && cropLookupKeys.Contains(d.Crop)));
            }

            if (!string.IsNullOrWhiteSpace(destination))
                query = query.Where(d => d.Destination == null || d.Destination == destination);

            var candidates = await query.ToListAsync(cancellationToken);

            var terms = System.Text.RegularExpressions.Regex
                .Matches(searchText.ToLowerInvariant(), @"[\p{L}\p{N}]+")
                .Select(match => match.Value)
                .Where(term => term.Length >= 2)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();

            if (terms.Length == 0)
                return [];

            var ranked = candidates
                .Select(document => new ComplianceSearchResult(
                    document,
                    Score(document, terms)))
                .Where(result => result.Score > 0)
                .OrderByDescending(r => r.Score)
                .Take(topK)
                .ToList();

            return ranked;
        }

        private static double Score(ComplianceDocument document, string[] terms)
        {
            var searchableText = string.Join(
                ' ',
                document.SourceName,
                document.SectionReference,
                document.Content,
                document.Crop,
                document.Destination);
            var matchedTerms = terms.Count(term =>
                searchableText.Contains(term, StringComparison.OrdinalIgnoreCase));

            return matchedTerms / (double)terms.Length;
        }
    }
}
