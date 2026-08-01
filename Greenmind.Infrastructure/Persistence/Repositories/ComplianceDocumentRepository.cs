using Greenmind.Application.Contracts.Persistence;
using Greenmind.Domain.Consts.Enums;
using Greenmind.Domain.Entities;
using Greenmind.Infrastructure.Rag;
using Microsoft.EntityFrameworkCore;

namespace Greenmind.Infrastructure.Persistence.Repositories
{
    public class ComplianceDocumentRepository(GreenmindDbContext context)
        : GenericRepository<ComplianceDocument>(context), IComplianceDocumentRepository
    {
        public async Task<List<ComplianceSearchResult>> SearchAsync(
            float[] queryEmbedding,
            ComplianceCategory? category = null,
            string? crop = null,
            string? destination = null,
            int topK = 5,
            CancellationToken cancellationToken = default)
        {
            // Pre-filter with SQL (cheap, indexed) before the expensive vector math.
            // Crop/Destination filters also match rows where the stored value is NULL,
            // since NULL means "applies to everything" (e.g. a shared EU-wide regulation).
            IQueryable<ComplianceDocument> query = DbSet.AsNoTracking();

            if (category is not null)
                query = query.Where(d => d.Category == category);

            if (!string.IsNullOrWhiteSpace(crop))
                query = query.Where(d => d.Crop == null || d.Crop == crop);

            if (!string.IsNullOrWhiteSpace(destination))
                query = query.Where(d => d.Destination == null || d.Destination == destination);

            var candidates = await query.ToListAsync(cancellationToken);

            // Brute-force ranking in memory. Fine for hundreds-to-low-thousands of chunks
            // (a single-crop project like Strawberry-only will likely stay well under that).
            var ranked = candidates
                .Select(doc => new
                {
                    Document = doc,
                    Embedding = EmbeddingSerializer.Deserialize(doc.Embedding)
                })
                // Embeddings created by the retired GitHub model have a different
                // dimension. Ignore them until the source document is re-ingested.
                .Where(item => item.Embedding.Length == queryEmbedding.Length)
                .Select(item => new ComplianceSearchResult(
                    item.Document,
                    VectorMath.CosineSimilarity(queryEmbedding, item.Embedding)))
                .OrderByDescending(r => r.Score)
                .Take(topK)
                .ToList();

            return ranked;
        }
    }
}
