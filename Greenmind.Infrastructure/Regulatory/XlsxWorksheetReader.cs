using System.Globalization;
using System.IO.Compression;
using System.Xml;
using System.Xml.Linq;
using Greenmind.Application.Contracts.Regulatory;

namespace Greenmind.Infrastructure.Regulatory;

internal sealed class XlsxWorksheetReader
{
    private const long MaximumExpandedEntryBytes = 50 * 1024 * 1024;
    private static readonly XNamespace SpreadsheetNamespace =
        "http://schemas.openxmlformats.org/spreadsheetml/2006/main";

    public XlsxTable ReadTable(Stream input, IReadOnlyCollection<string> requiredHeaders)
    {
        using var archive = new ZipArchive(input, ZipArchiveMode.Read, leaveOpen: true);
        var sharedStrings = ReadSharedStrings(archive);
        var worksheetEntries = archive.Entries
            .Where(entry => entry.FullName.StartsWith("xl/worksheets/sheet", StringComparison.OrdinalIgnoreCase)
                && entry.FullName.EndsWith(".xml", StringComparison.OrdinalIgnoreCase))
            .OrderBy(entry => entry.FullName, StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (worksheetEntries.Count == 0)
        {
            throw new RegulatoryImportValidationException("ملف Excel لا يحتوي على ورقة بيانات صالحة.");
        }

        foreach (var entry in worksheetEntries)
        {
            EnsureSafeEntry(entry);
            using var stream = entry.Open();
            var document = LoadDocument(stream);
            var rows = document
                .Descendants(SpreadsheetNamespace + "row")
                .Select(row => ReadRow(row, sharedStrings))
                .ToList();

            var headerRow = rows.FirstOrDefault(row => ContainsRequiredHeaders(row, requiredHeaders));
            if (headerRow is null)
            {
                continue;
            }

            var headerColumns = headerRow.Cells
                .Where(cell => !string.IsNullOrWhiteSpace(cell.Value))
                .GroupBy(cell => cell.Value!.Trim(), StringComparer.OrdinalIgnoreCase)
                .ToDictionary(group => group.Key, group => group.First().Key, StringComparer.OrdinalIgnoreCase);

            return new XlsxTable(
                headerColumns,
                rows.Where(row => row.RowNumber > headerRow.RowNumber).ToList());
        }

        throw new RegulatoryImportValidationException(
            $"لم يتم العثور على الأعمدة المطلوبة: {string.Join("، ", requiredHeaders)}.");
    }

    private static List<string> ReadSharedStrings(ZipArchive archive)
    {
        var entry = archive.GetEntry("xl/sharedStrings.xml");
        if (entry is null)
        {
            return [];
        }

        EnsureSafeEntry(entry);
        using var stream = entry.Open();
        var document = LoadDocument(stream);
        return document
            .Descendants(SpreadsheetNamespace + "si")
            .Select(item => string.Concat(item.Descendants(SpreadsheetNamespace + "t").Select(text => text.Value)))
            .ToList();
    }

    private static XlsxRow ReadRow(XElement rowElement, IReadOnlyList<string> sharedStrings)
    {
        var rowNumber = int.TryParse(
            rowElement.Attribute("r")?.Value,
            NumberStyles.None,
            CultureInfo.InvariantCulture,
            out var parsedRowNumber)
            ? parsedRowNumber
            : 0;

        var cells = new Dictionary<int, string?>();
        var fallbackColumn = 0;

        foreach (var cell in rowElement.Elements(SpreadsheetNamespace + "c"))
        {
            var reference = cell.Attribute("r")?.Value;
            var column = string.IsNullOrWhiteSpace(reference)
                ? fallbackColumn
                : GetColumnIndex(reference);
            fallbackColumn = column + 1;

            var type = cell.Attribute("t")?.Value;
            string? value;

            if (string.Equals(type, "inlineStr", StringComparison.OrdinalIgnoreCase))
            {
                value = string.Concat(cell.Descendants(SpreadsheetNamespace + "t").Select(text => text.Value));
            }
            else
            {
                value = cell.Element(SpreadsheetNamespace + "v")?.Value;
                if (string.Equals(type, "s", StringComparison.OrdinalIgnoreCase)
                    && int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var index)
                    && index >= 0
                    && index < sharedStrings.Count)
                {
                    value = sharedStrings[index];
                }
            }

            cells[column] = value;
        }

        return new XlsxRow(rowNumber, cells);
    }

    private static bool ContainsRequiredHeaders(XlsxRow row, IEnumerable<string> requiredHeaders)
    {
        var values = row.Cells.Values
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value!.Trim())
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        return requiredHeaders.All(values.Contains);
    }

    private static int GetColumnIndex(string cellReference)
    {
        var index = 0;
        foreach (var character in cellReference)
        {
            if (!char.IsLetter(character))
            {
                break;
            }

            index = (index * 26) + (char.ToUpperInvariant(character) - 'A' + 1);
        }

        return Math.Max(index - 1, 0);
    }

    private static XDocument LoadDocument(Stream stream)
    {
        using var reader = XmlReader.Create(stream, new XmlReaderSettings
        {
            DtdProcessing = DtdProcessing.Prohibit,
            XmlResolver = null,
            IgnoreComments = true,
            IgnoreProcessingInstructions = true
        });
        return XDocument.Load(reader, LoadOptions.None);
    }

    private static void EnsureSafeEntry(ZipArchiveEntry entry)
    {
        if (entry.Length > MaximumExpandedEntryBytes)
        {
            throw new RegulatoryImportValidationException("حجم البيانات داخل ملف Excel أكبر من الحد المسموح.");
        }
    }
}

internal sealed record XlsxTable(
    IReadOnlyDictionary<string, int> HeaderColumns,
    IReadOnlyList<XlsxRow> Rows)
{
    public string? GetValue(XlsxRow row, string header)
    {
        return HeaderColumns.TryGetValue(header, out var column)
            && row.Cells.TryGetValue(column, out var value)
            ? string.IsNullOrWhiteSpace(value) ? null : value.Trim()
            : null;
    }
}

internal sealed record XlsxRow(int RowNumber, IReadOnlyDictionary<int, string?> Cells);
