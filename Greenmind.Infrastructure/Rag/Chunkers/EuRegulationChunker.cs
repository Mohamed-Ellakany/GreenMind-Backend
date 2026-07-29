using System.Text.RegularExpressions;
using Greenmind.Application.Dtos.AI;
using Greenmind.Domain.Consts.Enums;

namespace Greenmind.Infrastructure.Rag.Chunkers
{
    /// <summary>
    /// Splits an EU regulation PDF (e.g. Regulation (EU) 2019/2072) into chunks aligned with
    /// its legal structure: "Article 5", "Article 6", ... and, where present, "ANNEX I", "ANNEX II".
    ///
    /// NOTE: Regulation 2019/2072's annexes are long species/pest listing tables (regulated pests
    /// per host genus). This generic Article/Annex splitter handles the body text well; the annex
    /// tables may need a dedicated tabular parser later if you want per-pest retrieval precision.
    /// For a strawberry-only project, prioritize ingesting the body Articles plus the specific
    /// annex subsection(s) that mention Fragaria / strawberries, rather than the entire annex set.
    /// </summary>
    public static class EuRegulationChunker
    {
        private static readonly Regex ArticleHeading =
            new(@"(?m)^Article\s+(?<num>\d+)\b", RegexOptions.Compiled);

        private static readonly Regex AnnexHeading =
            new(@"(?m)^ANNEX\s+(?<num>[IVXLC]+)\b", RegexOptions.Compiled);

        private const int MaxChunkChars = 1500;

        public static List<DocumentChunkDto> ChunkByArticles(string rawText, string sourceName)
        {
            var chunks = new List<DocumentChunkDto>();
            var matches = ArticleHeading.Matches(rawText);

            if (matches.Count == 0)
            {
                chunks.AddRange(SplitBySize(rawText, sourceName, sectionTitle: "Full text"));
                return chunks;
            }

            for (int i = 0; i < matches.Count; i++)
            {
                var start = matches[i].Index;
                var end = i + 1 < matches.Count ? matches[i + 1].Index : rawText.Length;
                var articleLabel = $"Article {matches[i].Groups["num"].Value}";
                var articleText = rawText[start..end].Trim();

                chunks.AddRange(articleText.Length <= MaxChunkChars
                    ? new[] { BuildChunk(articleText, sourceName, articleLabel) }
                    : SplitBySize(articleText, sourceName, articleLabel));
            }

            return chunks;
        }

        /// <summary>
        /// Extracts only the section(s) of an annex that mention a given keyword (e.g. "Fragaria"
        /// or "strawberr"), instead of ingesting the entire annex. Useful for a single-crop project
        /// where you don't want to pay embedding cost/noise for every host genus in the annex.
        /// </summary>
        public static List<DocumentChunkDto> ChunkAnnexFilteredByKeyword(
            string rawText, string sourceName, string annexLabelHint, params string[] keywords)
        {
            var chunks = new List<DocumentChunkDto>();
            var annexMatches = AnnexHeading.Matches(rawText);

            IEnumerable<(int Start, int End, string Label)> annexRanges = annexMatches.Count == 0
                ? new[] { (0, rawText.Length, annexLabelHint) }
                : annexMatches.Select((m, i) => (
                    m.Index,
                    i + 1 < annexMatches.Count ? annexMatches[i + 1].Index : rawText.Length,
                    $"ANNEX {m.Groups["num"].Value}"));

            foreach (var (start, end, label) in annexRanges)
            {
                var annexText = rawText[start..end];

                // Split into paragraphs and keep only ones mentioning our crop keywords,
                // plus a bit of surrounding context.
                var paragraphs = annexText.Split(["\n\n", "\r\n\r\n"], StringSplitOptions.RemoveEmptyEntries);

                foreach (var paragraph in paragraphs)
                {
                    if (keywords.Any(k => paragraph.Contains(k, StringComparison.OrdinalIgnoreCase)))
                    {
                        chunks.AddRange(paragraph.Length <= MaxChunkChars
                            ? new[] { BuildChunk(paragraph.Trim(), sourceName, label, ComplianceCategory.Phytosanitary) }
                            : SplitBySize(paragraph, sourceName, label));
                    }
                }
            }

            return chunks;
        }

        private static IEnumerable<DocumentChunkDto> SplitBySize(string text, string sourceName, string sectionTitle)
        {
            for (int offset = 0; offset < text.Length; offset += MaxChunkChars)
            {
                var length = Math.Min(MaxChunkChars, text.Length - offset);
                yield return BuildChunk(text.Substring(offset, length), sourceName, sectionTitle);
            }
        }

        private static DocumentChunkDto BuildChunk(
            string content, string sourceName, string sectionTitle,
            ComplianceCategory category = ComplianceCategory.Phytosanitary) => new()
            {
                Category = category,
                Crop = null, // EU-wide regulation text isn't crop-specific unless filtered (see annex method)
                Destination = null, // applies to all 6 EU destinations at once
                SourceName = sourceName,
                SectionReference = sectionTitle,
                Content = content.Trim()
            };
    }
}