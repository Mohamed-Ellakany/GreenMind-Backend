using Greenmind.Domain.Consts.Enums;
using Greenmind.Domain.Entities;

namespace Greenmind.Application.Contracts.Persistence
{
    public interface IComplianceDocumentRepository : IGenericRepository<ComplianceDocument>
    {
        /// <summary>
        /// Finds the top-K most semantically relevant chunks to the given query embedding,
        /// optionally pre-filtered by category and/or crop before ranking.
        /// </summary>
        Task<List<ComplianceSearchResult>> SearchAsync(
            float[] queryEmbedding,
            ComplianceCategory? category = null,
            string? crop = null,
            string? destination = null,
            int topK = 5,
            CancellationToken cancellationToken = default);
    }

    /// <summary>
    /// A retrieved chunk paired with its similarity score (0..1, higher = more relevant).
    /// </summary>
    public sealed record ComplianceSearchResult(ComplianceDocument Document, double Score);
}