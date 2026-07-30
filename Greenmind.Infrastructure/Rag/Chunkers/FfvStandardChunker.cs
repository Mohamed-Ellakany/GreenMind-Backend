using System.Text.RegularExpressions;
using Greenmind.Application.Dtos.AI;
using Greenmind.Domain.Consts.Enums;

namespace Greenmind.Infrastructure.Rag.Chunkers
{
    /// <summary>
    /// Splits a UNECE FFV standard (e.g. FFV-35 Strawberries) into chunks aligned with its
    /// natural structure: "I. Definition of produce", "II. Provisions concerning quality",
    /// "III. Provisions concerning sizing", etc. Falling back to lettered subsections
    /// (A., B., C. ...) for large sections so no chunk gets too big to embed meaningfully.
    /// </summary>
    public static class FfvStandardChunker
    {
        // Matches top-level headings like "I. Definition of produce" or "IV. Provisions concerning tolerances"
        private static readonly Regex TopLevelHeading =
            new(@"(?m)^(?<num>[IVX]+)\.\s+(?<title>[A-Z][^\n]{3,80})$", RegexOptions.Compiled);

        // Matches lettered subheadings like "A. Minimum requirements" or "(i) \u201cExtra\u201d Class"
        private static readonly Regex SubHeading =
            new(@"(?m)^(?<label>[A-Z]\.|\([ivx]+\))\s+(?<title>[^\n]{3,80})$", RegexOptions.Compiled);

        private const int MaxChunkChars = 1200; // ~250-350 tokens, keeps chunks focused

        public static List<DocumentChunkDto> Chunk(string rawText, string sourceName, string crop)
        {
            var chunks = new List<DocumentChunkDto>();
            var topMatches = TopLevelHeading.Matches(rawText);

            if (topMatches.Count == 0)
            {
                // Fallback: standard didn't match expected layout, keep it as one chunk
                // rather than silently dropping content.
                chunks.Add(new DocumentChunkDto
                {
                    Category = ComplianceCategory.QualityGrading,
                    Crop = crop,
                    SourceName = sourceName,
                    Content = rawText.Trim()
                });
                return chunks;
            }

            for (int i = 0; i < topMatches.Count; i++)
            {
                var start = topMatches[i].Index;
                var end = i + 1 < topMatches.Count ? topMatches[i + 1].Index : rawText.Length;
                var sectionTitle = $"{topMatches[i].Groups["num"].Value}. {topMatches[i].Groups["title"].Value}".Trim();
                var sectionText = rawText[start..end].Trim();

                if (sectionText.Length <= MaxChunkChars)
                {
                    chunks.Add(BuildChunk(sectionText, sourceName, crop, sectionTitle));
                    continue;
                }

                // Section too big (e.g. "Provisions concerning tolerances") -> split further by subheadings
                var subMatches = SubHeading.Matches(sectionText);
                if (subMatches.Count == 0)
                {
                    chunks.AddRange(SplitBySize(sectionText, sourceName, crop, sectionTitle));
                    continue;
                }

                for (int j = 0; j < subMatches.Count; j++)
                {
                    var subStart = subMatches[j].Index;
                    var subEnd = j + 1 < subMatches.Count ? subMatches[j + 1].Index : sectionText.Length;
                    var subTitle = $"{sectionTitle} - {subMatches[j].Groups["label"].Value} {subMatches[j].Groups["title"].Value}".Trim();
                    var subText = sectionText[subStart..subEnd].Trim();

                    chunks.AddRange(subText.Length <= MaxChunkChars
                        ? new[] { BuildChunk(subText, sourceName, crop, subTitle) }
                        : SplitBySize(subText, sourceName, crop, subTitle));
                }
            }

            return chunks;
        }

        private static IEnumerable<DocumentChunkDto> SplitBySize(string text, string sourceName, string crop, string sectionTitle)
        {
            for (int offset = 0; offset < text.Length; offset += MaxChunkChars)
            {
                var length = Math.Min(MaxChunkChars, text.Length - offset);
                yield return BuildChunk(text.Substring(offset, length), sourceName, crop, sectionTitle);
            }
        }

        private static DocumentChunkDto BuildChunk(string content, string sourceName, string crop, string sectionTitle) => new()
        {
            Category = ComplianceCategory.QualityGrading,
            Crop = crop,
            SourceName = sourceName,
            SectionReference = sectionTitle,
            Content = content
        };
    }
}