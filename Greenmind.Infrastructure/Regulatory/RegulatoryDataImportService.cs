using System.Globalization;
using System.IO.Compression;
using System.Net;
using System.Text.RegularExpressions;
using System.Xml;
using Greenmind.Application.Contracts.Regulatory;
using Greenmind.Application.Dtos.Regulatory;
using Greenmind.Domain.Consts.Enums;
using Greenmind.Domain.Entities;
using Greenmind.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace Greenmind.Infrastructure.Regulatory;

public sealed partial class RegulatoryDataImportService(
    GreenmindDbContext context,
    TimeProvider timeProvider) : IRegulatoryDataImportService
{
    private const long MaximumFileSize = 10 * 1024 * 1024;
    private static readonly string[] ActiveSubstanceRequiredHeaders =
    [
        "Active Substance ID",
        "Substance",
        "Status under Reg. (EC) No 1107/2009"
    ];

    private static readonly string[] MrlRequiredHeaders =
    [
        "Pesticide Id",
        "Pesticide residue",
        "Maximum residue level (mg/kg)"
    ];

    public async Task<RegulatoryImportResultDto> ImportActiveSubstancesAsync(
        string regionName,
        string fileName,
        long fileLength,
        Stream fileStream,
        CancellationToken cancellationToken = default)
    {
        var canonicalRegion = await ResolveDestinationNameAsync(regionName, cancellationToken);
        await using var workbook = await ValidateAndCopyWorkbookAsync(
            fileName,
            fileLength,
            fileStream,
            cancellationToken);

        XlsxTable table;
        try
        {
            table = new XlsxWorksheetReader().ReadTable(workbook, ActiveSubstanceRequiredHeaders);
        }
        catch (RegulatoryImportValidationException)
        {
            throw;
        }
        catch (Exception exception) when (exception is InvalidDataException or XmlException)
        {
            throw new RegulatoryImportValidationException("تعذر قراءة ملف Excel. تأكد من أن الملف غير تالف.");
        }

        var parsed = ParseActiveSubstances(table, canonicalRegion);
        if (parsed.Items.Count == 0)
        {
            throw new RegulatoryImportValidationException("لا يحتوي الملف على مواد فعالة قابلة للاستيراد.");
        }

        await ReplaceActiveSubstancesAsync(canonicalRegion, parsed.Items, cancellationToken);
        return new RegulatoryImportResultDto(
            "تم استيراد بيانات المواد الفعالة بنجاح",
            parsed.Items.Count,
            parsed.SkippedRows);
    }

    public async Task<RegulatoryImportResultDto> ImportProductMrlsAsync(
        string regionName,
        string productType,
        string fileName,
        long fileLength,
        Stream fileStream,
        CancellationToken cancellationToken = default)
    {
        var canonicalRegion = await ResolveDestinationNameAsync(regionName, cancellationToken);
        var canonicalProduct = await ResolveCropNameAsync(productType, cancellationToken);
        await using var workbook = await ValidateAndCopyWorkbookAsync(
            fileName,
            fileLength,
            fileStream,
            cancellationToken);

        XlsxTable table;
        try
        {
            table = new XlsxWorksheetReader().ReadTable(workbook, MrlRequiredHeaders);
        }
        catch (RegulatoryImportValidationException)
        {
            throw;
        }
        catch (Exception exception) when (exception is InvalidDataException or XmlException)
        {
            throw new RegulatoryImportValidationException("تعذر قراءة ملف Excel. تأكد من أن الملف غير تالف.");
        }

        var parsed = ParseProductMrls(table, canonicalRegion, canonicalProduct);
        if (parsed.Items.Count == 0)
        {
            throw new RegulatoryImportValidationException("لا يحتوي الملف على حدود متبقيات قابلة للاستيراد.");
        }

        await ReplaceProductMrlsAsync(canonicalRegion, canonicalProduct, parsed.Items, cancellationToken);
        return new RegulatoryImportResultDto(
            "تم استيراد بيانات الحدود القصوى للمتبقيات بنجاح",
            parsed.Items.Count,
            parsed.SkippedRows);
    }

    private async Task ReplaceActiveSubstancesAsync(
        string regionName,
        IReadOnlyCollection<ActiveSubstance> items,
        CancellationToken cancellationToken)
    {
        await using var transaction = await BeginTransactionIfSupportedAsync(cancellationToken);
        try
        {
            var now = timeProvider.GetUtcNow().UtcDateTime;
            var currentItems = await context.ActiveSubstances
                .Where(item => item.RegionName == regionName && !item.IsDeleted)
                .ToListAsync(cancellationToken);

            foreach (var item in currentItems)
            {
                item.IsDeleted = true;
                item.DeletedAt = now;
            }

            await context.SaveChangesAsync(cancellationToken);
            await context.ActiveSubstances.AddRangeAsync(items, cancellationToken);
            await context.SaveChangesAsync(cancellationToken);

            if (transaction is not null)
            {
                await transaction.CommitAsync(cancellationToken);
            }
        }
        catch
        {
            if (transaction is not null)
            {
                await transaction.RollbackAsync(cancellationToken);
            }

            throw;
        }
    }

    private async Task ReplaceProductMrlsAsync(
        string regionName,
        string productType,
        IReadOnlyCollection<ProductMrl> items,
        CancellationToken cancellationToken)
    {
        await using var transaction = await BeginTransactionIfSupportedAsync(cancellationToken);
        try
        {
            var now = timeProvider.GetUtcNow().UtcDateTime;
            var currentItems = await context.ProductMrls
                .Where(item => item.RegionName == regionName
                    && item.ProductType == productType
                    && !item.IsDeleted)
                .ToListAsync(cancellationToken);

            foreach (var item in currentItems)
            {
                item.IsDeleted = true;
                item.DeletedAt = now;
            }

            await context.SaveChangesAsync(cancellationToken);
            await context.ProductMrls.AddRangeAsync(items, cancellationToken);
            await context.SaveChangesAsync(cancellationToken);

            if (transaction is not null)
            {
                await transaction.CommitAsync(cancellationToken);
            }
        }
        catch
        {
            if (transaction is not null)
            {
                await transaction.RollbackAsync(cancellationToken);
            }

            throw;
        }
    }

    private ParsedRows<ActiveSubstance> ParseActiveSubstances(XlsxTable table, string regionName)
    {
        var itemsByExternalId = new Dictionary<int, ActiveSubstance>();
        var skippedRows = 0;
        var now = timeProvider.GetUtcNow().UtcDateTime;

        foreach (var row in table.Rows)
        {
            var externalIdText = table.GetValue(row, "Active Substance ID");
            var name = table.GetValue(row, "Substance");
            var statusText = table.GetValue(row, "Status under Reg. (EC) No 1107/2009");

            if (externalIdText is null && name is null && statusText is null)
            {
                continue;
            }

            if (!int.TryParse(externalIdText, NumberStyles.Integer, CultureInfo.InvariantCulture, out var externalId))
            {
                throw RowError(row, "رقم المادة الفعالة غير صحيح.");
            }

            if (string.IsNullOrWhiteSpace(name))
            {
                throw RowError(row, "اسم المادة الفعالة مطلوب.");
            }

            var item = new ActiveSubstance
            {
                RegionName = regionName,
                ExternalId = externalId,
                Name = EnsureMaxLength(name, 500, row, "اسم المادة الفعالة"),
                CasNumber = OptionalMaxLength(table.GetValue(row, "CAS Number"), 100, row, "CAS Number"),
                ApprovalStatus = ParseApprovalStatus(statusText, row),
                ApprovalExpiresOn = ParseDate(table.GetValue(row, "Expiration of approval"), row),
                Legislation = OptionalMaxLength(table.GetValue(row, "Legislation"), 1000, row, "Legislation"),
                Remark = SanitizeRemark(table.GetValue(row, "Remark")),
                RegulationReference = OptionalMaxLength(table.GetValue(row, "Reg 396/2005"), 1000, row, "Reg 396/2005"),
                Functions = OptionalMaxLength(table.GetValue(row, "Functions"), 1000, row, "Functions"),
                CreatedAt = now
            };

            if (itemsByExternalId.TryGetValue(externalId, out var existing))
            {
                if (!ActiveSubstanceRowsMatch(existing, item))
                {
                    throw RowError(row, $"رقم المادة الفعالة {externalId} مكرر بقيم مختلفة.");
                }

                skippedRows++;
                continue;
            }

            itemsByExternalId.Add(externalId, item);
        }

        return new ParsedRows<ActiveSubstance>(itemsByExternalId.Values.ToList(), skippedRows);
    }

    private ParsedRows<ProductMrl> ParseProductMrls(
        XlsxTable table,
        string regionName,
        string productType)
    {
        var items = new List<ProductMrl>();
        var uniqueRows = new HashSet<string>(StringComparer.Ordinal);
        var skippedRows = 0;
        var now = timeProvider.GetUtcNow().UtcDateTime;

        foreach (var row in table.Rows)
        {
            var pesticideId = table.GetValue(row, "Pesticide Id");
            var residueName = table.GetValue(row, "Pesticide residue");
            var rawMrl = table.GetValue(row, "Maximum residue level (mg/kg)");

            if (pesticideId is null && residueName is null && rawMrl is null)
            {
                continue;
            }

            if (string.IsNullOrWhiteSpace(pesticideId))
            {
                throw RowError(row, "رقم المبيد مطلوب.");
            }

            if (string.IsNullOrWhiteSpace(residueName))
            {
                throw RowError(row, "اسم متبقي المبيد مطلوب.");
            }

            var isDetectionLimit = rawMrl?.Contains('*', StringComparison.Ordinal) == true;
            var numericText = rawMrl?.Replace("*", string.Empty, StringComparison.Ordinal).Trim();
            decimal? maximumResidueLevel = null;

            if (!string.IsNullOrWhiteSpace(numericText))
            {
                if (!decimal.TryParse(
                        numericText,
                        NumberStyles.Number,
                        CultureInfo.InvariantCulture,
                        out var parsedValue)
                    || parsedValue < 0)
                {
                    throw RowError(row, "قيمة الحد الأقصى للمتبقيات غير صحيحة.");
                }

                maximumResidueLevel = parsedValue;
            }

            var canonicalPesticideId = EnsureMaxLength(pesticideId, 50, row, "رقم المبيد");
            var canonicalResidueName = residueName.Trim();
            var duplicateKey = string.Join(
                '\u001f',
                canonicalPesticideId,
                canonicalResidueName,
                maximumResidueLevel?.ToString(CultureInfo.InvariantCulture) ?? string.Empty,
                isDetectionLimit ? "1" : "0");

            if (!uniqueRows.Add(duplicateKey))
            {
                skippedRows++;
                continue;
            }

            items.Add(new ProductMrl
            {
                RegionName = regionName,
                ProductType = productType,
                ExternalPesticideId = canonicalPesticideId,
                ResidueName = canonicalResidueName,
                MaximumResidueLevel = maximumResidueLevel,
                IsDetectionLimit = isDetectionLimit,
                Unit = "mg/kg",
                CreatedAt = now
            });
        }

        return new ParsedRows<ProductMrl>(items, skippedRows);
    }

    private async Task<string> ResolveDestinationNameAsync(
        string regionName,
        CancellationToken cancellationToken)
    {
        var normalized = NormalizeSelection(regionName, "المنطقة");
        var upper = normalized.ToUpperInvariant();
        var destination = await context.Destinations
            .AsNoTracking()
            .FirstOrDefaultAsync(item => !item.IsDeleted && item.IsActive && item.Name.ToUpper() == upper, cancellationToken);

        return destination?.Name
            ?? throw new RegulatoryImportValidationException("المنطقة المختارة غير موجودة أو غير نشطة.");
    }

    private async Task<string> ResolveCropNameAsync(
        string productType,
        CancellationToken cancellationToken)
    {
        var normalized = NormalizeSelection(productType, "المنتج");
        var upper = normalized.ToUpperInvariant();
        var crop = await context.Crops
            .AsNoTracking()
            .FirstOrDefaultAsync(item => !item.IsDeleted && item.IsActive && item.Name.ToUpper() == upper, cancellationToken);

        return crop?.Name
            ?? throw new RegulatoryImportValidationException("المنتج المختار غير موجود أو غير نشط.");
    }

    private static async Task<MemoryStream> ValidateAndCopyWorkbookAsync(
        string fileName,
        long fileLength,
        Stream fileStream,
        CancellationToken cancellationToken)
    {
        if (!string.Equals(Path.GetExtension(fileName), ".xlsx", StringComparison.OrdinalIgnoreCase))
        {
            throw new RegulatoryImportValidationException("يسمح برفع ملفات Excel بامتداد .xlsx فقط.");
        }

        if (fileLength <= 0)
        {
            throw new RegulatoryImportValidationException("ملف Excel فارغ.");
        }

        if (fileLength > MaximumFileSize)
        {
            throw new RegulatoryImportValidationException("حجم ملف Excel يجب ألا يتجاوز 10 ميجابايت.");
        }

        var copy = new MemoryStream((int)fileLength);
        await fileStream.CopyToAsync(copy, cancellationToken);
        if (copy.Length != fileLength || copy.Length > MaximumFileSize)
        {
            await copy.DisposeAsync();
            throw new RegulatoryImportValidationException("تعذر التحقق من حجم ملف Excel.");
        }

        copy.Position = 0;
        return copy;
    }

    private async Task<IDbContextTransaction?> BeginTransactionIfSupportedAsync(CancellationToken cancellationToken)
    {
        return context.Database.IsRelational()
            ? await context.Database.BeginTransactionAsync(cancellationToken)
            : null;
    }

    private static ActiveSubstanceApprovalStatus ParseApprovalStatus(string? value, XlsxRow row)
    {
        return value?.Trim().ToUpperInvariant() switch
        {
            "PENDING" => ActiveSubstanceApprovalStatus.قيد_المراجعة,
            "NOT APPROVED" => ActiveSubstanceApprovalStatus.غير_معتمد,
            "APPROVED" => ActiveSubstanceApprovalStatus.معتمد,
            _ => throw RowError(row, "حالة اعتماد المادة الفعالة غير معروفة.")
        };
    }

    private static DateOnly? ParseDate(string? value, XlsxRow row)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        string[] formats = ["d/M/yyyy", "dd/MM/yyyy", "yyyy-MM-dd"];
        if (DateOnly.TryParseExact(
                value.Trim(),
                formats,
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out var date))
        {
            return date;
        }

        throw RowError(row, "تاريخ انتهاء الاعتماد غير صحيح.");
    }

    private static string? SanitizeRemark(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var withoutHtml = HtmlTagRegex().Replace(value, " ");
        return WhitespaceRegex().Replace(WebUtility.HtmlDecode(withoutHtml), " ").Trim();
    }

    private static string NormalizeSelection(string? value, string fieldName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new RegulatoryImportValidationException($"اختيار {fieldName} مطلوب.");
        }

        var normalized = value.Trim();
        if (normalized.Length > 200)
        {
            throw new RegulatoryImportValidationException($"اسم {fieldName} أطول من الحد المسموح.");
        }

        return normalized;
    }

    private static string EnsureMaxLength(
        string value,
        int maximumLength,
        XlsxRow row,
        string fieldName)
    {
        var normalized = value.Trim();
        if (normalized.Length > maximumLength)
        {
            throw RowError(row, $"قيمة {fieldName} أطول من الحد المسموح.");
        }

        return normalized;
    }

    private static string? OptionalMaxLength(
        string? value,
        int maximumLength,
        XlsxRow row,
        string fieldName)
    {
        return string.IsNullOrWhiteSpace(value)
            ? null
            : EnsureMaxLength(value, maximumLength, row, fieldName);
    }

    private static RegulatoryImportValidationException RowError(XlsxRow row, string message) =>
        new($"الصف {row.RowNumber}: {message}");

    private static bool ActiveSubstanceRowsMatch(ActiveSubstance left, ActiveSubstance right) =>
        left.RegionName == right.RegionName
        && left.ExternalId == right.ExternalId
        && left.Name == right.Name
        && left.CasNumber == right.CasNumber
        && left.ApprovalStatus == right.ApprovalStatus
        && left.ApprovalExpiresOn == right.ApprovalExpiresOn
        && left.Legislation == right.Legislation
        && left.Remark == right.Remark
        && left.RegulationReference == right.RegulationReference
        && left.Functions == right.Functions;

    [GeneratedRegex("<[^>]+>", RegexOptions.CultureInvariant)]
    private static partial Regex HtmlTagRegex();

    [GeneratedRegex("\\s+", RegexOptions.CultureInvariant)]
    private static partial Regex WhitespaceRegex();

    private sealed record ParsedRows<T>(IReadOnlyList<T> Items, int SkippedRows);
}
