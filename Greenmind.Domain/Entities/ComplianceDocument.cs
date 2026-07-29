using System.ComponentModel.DataAnnotations;
using Greenmind.Domain.Consts.Enums;

namespace Greenmind.Domain.Entities
{
    /// <summary>
    /// A single retrievable chunk of compliance/regulatory/agronomic knowledge.
    /// One row = one chunk (not one whole document) so it can be embedded and
    /// retrieved independently by the RAG plugin.
    /// </summary>
    public class ComplianceDocument : BaseEntity
    {
        [Required]
        public ComplianceCategory Category { get; set; }

        /// <summary>
        /// Crop this chunk applies to (e.g. "Strawberry"). Null = applies to all crops
        /// (e.g. general EU phytosanitary regulation text that isn't crop-specific).
        /// </summary>
        [MaxLength(100)]
        public string? Crop { get; set; }

        /// <summary>
        /// Destination country this chunk applies to. Null = applies to all destinations
        /// (e.g. EU-wide phytosanitary regulation covers all 6 destinations at once).
        /// </summary>
        [MaxLength(100)]
        public string? Destination { get; set; }

        /// <summary>
        /// Human-readable origin, e.g. "UNECE FFV-35 (2023)" or "EU Regulation 2019/2072".
        /// Shown back to the agent/user for traceability.
        /// </summary>
        [Required]
        [MaxLength(200)]
        public required string SourceName { get; set; }

        /// <summary>
        /// Where in the source this chunk came from, e.g. "Section III - Sizing" or "Article 5".
        /// Helps humans (and the agent) cite precisely instead of just "somewhere in the PDF".
        /// </summary>
        [MaxLength(200)]
        public string? SectionReference { get; set; }

        /// <summary>
        /// The actual chunk text that gets embedded and shown to the LLM at retrieval time.
        /// </summary>
        [Required]
        public required string Content { get; set; }

        /// <summary>
        /// Embedding vector, serialized as a JSON float array string (e.g. "[0.012,-0.084,...]").
        /// Stored as text for simplicity/portability at this data volume (a few hundred chunks).
        /// If this grows into the tens of thousands of chunks, migrate to a dedicated vector
        /// store (SQL Server native vector type, pgvector, Qdrant, Azure AI Search) instead of
        /// brute-force in-memory cosine similarity.
        /// </summary>
        [Required]
        public required string Embedding { get; set; }
    }
}