using Greenmind.Domain.Consts.Enums;

namespace Greenmind.Application.Dtos.AI
{
    /// <summary>
    /// A raw, not-yet-embedded chunk produced by a chunker, ready to be embedded
    /// and persisted as a ComplianceDocument.
    /// </summary>
    public class DocumentChunkDto
    {
        public ComplianceCategory Category { get; set; }
        public string? Crop { get; set; }
        public string? Destination { get; set; }
        public required string SourceName { get; set; }
        public string? SectionReference { get; set; }
        public required string Content { get; set; }
    }
}