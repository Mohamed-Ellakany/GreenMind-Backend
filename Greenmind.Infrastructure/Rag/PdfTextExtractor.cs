using System.Text;
using UglyToad.PdfPig;
using UglyToad.PdfPig.DocumentLayoutAnalysis.TextExtractor;

namespace Greenmind.Infrastructure.Rag
{
    /// <summary>
    /// Extracts plain text from a PDF, page by page, in reading order.
    /// Requires NuGet package: UglyToad.PdfPig
    ///
    /// IMPORTANT: page.Text (the simple property) concatenates words WITHOUT reliable line
    /// breaks, which breaks the chunkers' "^Article 5" / "^I. Definition" regex matching
    /// (they rely on headings starting a new line). Use ContentOrderTextExtractor instead,
    /// which reconstructs lines from word bounding boxes -> much more reliable for
    /// structured legal/standard documents like FFV-35 or EU Regulation 2019/2072.
    /// </summary>
    public static class PdfTextExtractor
    {
        public static string ExtractText(Stream pdfStream)
        {
            using var document = PdfDocument.Open(pdfStream);
            var sb = new StringBuilder();

            foreach (var page in document.GetPages())
            {
                var pageText = ContentOrderTextExtractor.GetText(page);
                sb.AppendLine(pageText);
                sb.AppendLine(); // page break -> paragraph break, helps chunkers
            }

            return sb.ToString();
        }

        public static string ExtractText(string filePath)
        {
            using var stream = File.OpenRead(filePath);
            return ExtractText(stream);
        }
    }
}