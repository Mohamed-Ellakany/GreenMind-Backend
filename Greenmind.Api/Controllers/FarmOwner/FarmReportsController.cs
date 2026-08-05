using System.Globalization;
using System.Reflection;
using System.Security.Claims;
using System.Text;
using ClosedXML.Excel;
using Greenmind.Domain.Consts;
using Greenmind.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QuestPDF.Drawing;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace Greenmind.Api.Controllers.FarmOwner;

[ApiController]
[Route("api/farm-owner/reports")]
[Authorize(Roles = RoleNames.FarmOwner)]
public sealed class FarmReportsController(GreenmindDbContext db) : ControllerBase
{
    private const string ArabicFontFamily = "Noto Sans Arabic";
    private static readonly object PdfConfigurationLock = new();
    private static bool _pdfConfigured;

    [HttpGet("export")]
    public async Task<IActionResult> Export([FromQuery] FarmReportRequest request, CancellationToken cancellationToken)
    {
        if (request.From.HasValue && request.To.HasValue && request.From > request.To)
            return BadRequest(new { message = "تاريخ البداية يجب أن يسبق تاريخ النهاية." });

        var ownerId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        var plans = db.ExportPlans.AsNoTracking().Where(plan => plan.Farm.OwnerUserId == ownerId);
        if (request.ExportPlanId is int id) plans = plans.Where(plan => plan.Id == id);
        var planRows = await plans
            .Include(plan => plan.Farm)
            .Include(plan => plan.Crop)
            .Include(plan => plan.Destination)
            .ToListAsync(cancellationToken);
        if (request.ExportPlanId.HasValue && planRows.Count == 0)
            return NotFound(new { message = "Export plan was not found." });

        var ids = planRows.Select(plan => plan.Id).ToHashSet();
        var farmIds = planRows.Select(plan => plan.FarmId).ToHashSet();
        var from = request.From?.ToDateTime(TimeOnly.MinValue); var to = request.To?.ToDateTime(TimeOnly.MaxValue);
        string? selectedExpenseCategory = null;
        if (request.ExpenseCategoryId is int requestedCategoryId)
        {
            selectedExpenseCategory = await db.ExpenseCategories
                .AsNoTracking()
                .Where(category => category.Id == requestedCategoryId && farmIds.Contains(category.FarmId))
                .Select(category => category.Name)
                .SingleOrDefaultAsync(cancellationToken);
            if (selectedExpenseCategory is null)
                return BadRequest(new { message = "فئة المصروف المحددة غير متاحة لهذه المزرعة." });
        }

        var expenseQuery = db.Expenses.AsNoTracking()
            .Include(item => item.ExpenseCategory)
            .Include(item => item.ExportPlan)
            .Include(item => item.Attachments)
            .Where(item => ids.Contains(item.ExportPlanId) && !item.IsDeleted && (!from.HasValue || item.ExpenseDate >= DateOnly.FromDateTime(from.Value)) && (!to.HasValue || item.ExpenseDate <= DateOnly.FromDateTime(to.Value)));
        if (request.ExpenseCategoryId is int categoryId)
            expenseQuery = expenseQuery.Where(item => item.ExpenseCategoryId == categoryId);
        var expenses = await expenseQuery
            .OrderByDescending(item => item.ExpenseDate)
            .ThenByDescending(item => item.Id)
            .ToListAsync(cancellationToken);
        var tasks = await db.FarmTasks.AsNoTracking().Include(item => item.ExportPlan).Where(item => ids.Contains(item.ExportPlanId) && (!from.HasValue || item.PlannedDate >= DateOnly.FromDateTime(from.Value)) && (!to.HasValue || item.PlannedDate <= DateOnly.FromDateTime(to.Value))).ToListAsync(cancellationToken);
        var selected = request.Sections?.Length > 0
            ? request.Sections.SelectMany(value => value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)).ToArray()
            : ["plans", "tasks", "spending"];
        var report = new ReportData(
            planRows,
            tasks,
            expenses,
            selected,
            planRows.FirstOrDefault()?.Farm.Name ?? "GreenMind Farm",
            request.From,
            request.To,
            selectedExpenseCategory,
            DateTime.UtcNow);
        var reportKind = report.Has("spending") && report.Sections.Length == 1 ? "expenses" : "management";
        var name = $"greenmind-{reportKind}-report-{DateTime.UtcNow:yyyyMMdd}";
        if (string.Equals(request.Format, "pdf", StringComparison.OrdinalIgnoreCase))
            ConfigurePdf();
        return request.Format.ToLowerInvariant() switch
        {
            "csv" => File(Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(ToCsv(report))).ToArray(), "text/csv; charset=utf-8", name + ".csv"),
            "xlsx" => File(ToExcel(report), "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", name + ".xlsx"),
            "pdf" => File(ToPdf(report), "application/pdf", name + ".pdf"),
            _ => BadRequest(new { message = "format must be pdf, xlsx, or csv." })
        };
    }

    private static string ToCsv(ReportData report)
    {
        var lines = new List<string> { "Section,Export plan,Date,Description,Category,Amount,Status" };
        if (report.Has("plans")) lines.AddRange(report.Plans.Select(plan => $"Plan,{Q(plan.Name)},{plan.ExpectedExportDate},Crop: {Q(plan.Crop.Name)},,,{plan.Status}"));
        if (report.Has("tasks")) lines.AddRange(report.Tasks.Select(task => $"Task,{Q(task.ExportPlan.Name)},{task.PlannedDate},{Q(task.Title)},,,{task.Status}"));
        if (report.Has("spending")) lines.AddRange(report.Expenses.Select(item => $"Spending,{Q(item.ExportPlan.Name)},{item.ExpenseDate},{Q(item.Description)},{Q(item.ExpenseCategory.Name)},{item.Amount.ToString(CultureInfo.InvariantCulture)},"));
        return string.Join("\r\n", lines);
    }
    private static byte[] ToExcel(ReportData report)
    {
        using var book = new XLWorkbook();
        if (report.Has("plans")) AddSheet(book, "Export Plans", ["Name", "Crop", "Destination", "Export Date", "Status"], report.Plans.Select(plan => new object?[] { plan.Name, plan.Crop.Name, plan.Destination.Name, plan.ExpectedExportDate.ToString("yyyy-MM-dd"), plan.Status.ToString() }));
        if (report.Has("tasks")) AddSheet(book, "Tasks", ["Plan", "Title", "Date", "Status"], report.Tasks.Select(task => new object?[] { task.ExportPlan.Name, task.Title, task.PlannedDate.ToString("yyyy-MM-dd"), task.Status.ToString() }));
        if (report.Has("spending")) AddSheet(book, "Spendings", ["Plan", "Date", "Description", "Category", "Amount"], report.Expenses.Select(item => new object?[] { item.ExportPlan.Name, item.ExpenseDate.ToString("yyyy-MM-dd"), item.Description, item.ExpenseCategory.Name, item.Amount }));
        using var stream = new MemoryStream(); book.SaveAs(stream); return stream.ToArray();
    }
    private static void AddSheet(XLWorkbook book, string name, string[] headers, IEnumerable<object?[]> rows) { var sheet = book.Worksheets.Add(name); for (var i = 0; i < headers.Length; i++) sheet.Cell(1, i + 1).Value = headers[i]; sheet.Row(1).Style.Font.Bold = true; var row = 2; foreach (var values in rows) { for (var column = 0; column < values.Length; column++) sheet.Cell(row, column + 1).Value = values[column]?.ToString(); row++; } sheet.Columns().AdjustToContents(); }
    private static byte[] ToPdf(ReportData report) => Document.Create(document =>
    {
        document.Page(page =>
        {
            page.Size(PageSizes.A4.Landscape());
            page.MarginHorizontal(30);
            page.MarginVertical(24);
            page.ContentFromRightToLeft();
            page.DefaultTextStyle(style => style
                .FontFamily(ArabicFontFamily)
                .FontSize(9)
                .FontColor("#24352B")
                .DirectionFromRightToLeft());

            page.Header().PaddingBottom(14).Row(row =>
            {
                row.ConstantItem(54).Height(54).Background("#E8F6EC").Padding(9).Svg(BrandLogoSvg);
                row.RelativeItem().PaddingRight(12).Column(column =>
                {
                    column.Item().Text(report.CompanyName).ClampLines(1, "...").FontSize(9).SemiBold().FontColor("#168148");
                    column.Item().PaddingTop(2).Text(report.Title).FontSize(21).Bold().FontColor("#092B19");
                    column.Item().PaddingTop(3).Text($"خطة التصدير: {report.PlanLabel}").ClampLines(1, "...").FontSize(9).FontColor("#64746A");
                });
                row.ConstantItem(210).AlignLeft().Column(column =>
                {
                    column.Item().AlignLeft().Text("GREENMIND").FontSize(10).Bold().FontColor("#0B6B38").DirectionFromLeftToRight();
                    column.Item().PaddingTop(4).AlignLeft().Text($"الفترة: {report.PeriodLabel}").FontSize(8).FontColor("#64746A");
                    column.Item().PaddingTop(2).AlignLeft().Text($"الفئة: {report.CategoryLabel}").ClampLines(2, "...").FontSize(8).FontColor("#64746A");
                });
            });

            page.Content().Column(column =>
            {
                column.Spacing(12);
                column.Item().Element(container => ComposeSummary(container, report));

                if (report.Has("spending"))
                    ComposeExpenseSection(column, report);

                if (report.Has("tasks"))
                {
                    column.Item().Element(container => SectionHeading(container, "المهام", "ملخص المهام الواقعة داخل فترة التقرير"));
                    foreach (var task in report.Tasks)
                        column.Item().Background("#FAFCFA").BorderBottom(1).BorderColor("#E4ECE6").Padding(7)
                            .Text($"{task.PlannedDate:yyyy/MM/dd} - {task.Title} - {task.Status}");
                }

                if (report.Has("plans"))
                {
                    column.Item().Element(container => SectionHeading(container, "خطط التصدير", "الخطط المشمولة في نطاق التقرير"));
                    foreach (var plan in report.Plans)
                        column.Item().Background("#FAFCFA").BorderBottom(1).BorderColor("#E4ECE6").Padding(7)
                            .Text($"{plan.Name} - {plan.Crop.Name} - {plan.Destination.Name} - {plan.Status}");
                }
            });

            page.Footer().PaddingTop(9).BorderTop(1).BorderColor("#DCE8DF").Row(row =>
            {
                row.RelativeItem().Text($"أُنشئ بواسطة GreenMind - {report.GeneratedAtUtc:yyyy/MM/dd HH:mm} UTC")
                    .FontSize(7.5f).FontColor("#75847B");
                row.AutoItem().Text(text =>
                {
                    text.DefaultTextStyle(style => style.FontSize(7.5f).FontColor("#75847B"));
                    text.Span("صفحة ");
                    text.CurrentPageNumber();
                    text.Span(" من ");
                    text.TotalPages();
                });
            });
        });
    }).GeneratePdf();

    private static void ComposeSummary(IContainer container, ReportData report)
    {
        var total = report.Expenses.Sum(item => item.Amount);
        var totalArea = report.Plans.Sum(plan => plan.AreaInFeddan);
        var costPerFeddan = totalArea <= 0 ? 0 : total / totalArea;
        var highestCategory = report.Expenses
            .GroupBy(item => item.ExpenseCategory.Name)
            .Select(group => new { Name = group.Key, Total = group.Sum(item => item.Amount) })
            .OrderByDescending(item => item.Total)
            .FirstOrDefault();

        container.Background("#F2F8F4").Border(1).BorderColor("#D8E7DC").PaddingVertical(11).PaddingHorizontal(8).Row(row =>
        {
            row.RelativeItem().Element(item => SummaryMetric(item, "عدد المصروفات", FormatNumber(report.Expenses.Count)));
            row.RelativeItem().Element(item => SummaryMetric(item, "إجمالي المصروفات", FormatMoney(total), accent: true, leftToRight: true));
            row.RelativeItem().Element(item => SummaryMetric(item, "تكلفة الفدان", FormatMoney(costPerFeddan), leftToRight: true));
            row.RelativeItem().Element(item => SummaryMetric(item, "أعلى فئة", highestCategory?.Name ?? "لا توجد بيانات"));
        });
    }

    private static void ComposeExpenseSection(ColumnDescriptor column, ReportData report)
    {
        var grandTotal = report.Expenses.Sum(item => item.Amount);
        var categoryTotals = report.Expenses
            .GroupBy(item => item.ExpenseCategory.Name)
            .Select(group => new CategoryTotal(group.Key, group.Sum(item => item.Amount)))
            .OrderByDescending(item => item.Total)
            .ToArray();
        var displayedCategories = categoryTotals.Length <= 6
            ? categoryTotals
            : categoryTotals.Take(5).Append(new CategoryTotal("فئات أخرى", categoryTotals.Skip(5).Sum(item => item.Total))).ToArray();

        column.Item().Element(container => SectionHeading(container, "تحليل الإنفاق حسب الفئة", "ترتيب الفئات من الأعلى إنفاقًا داخل الفترة المحددة"));
        if (displayedCategories.Length > 0)
        {
            column.Item().Background("#FBFDFC").Border(1).BorderColor("#E1EAE3").Padding(10).Column(categories =>
            {
                categories.Spacing(7);
                foreach (var category in displayedCategories)
                {
                    var percentage = grandTotal <= 0 ? 0 : decimal.Round(category.Total / grandTotal * 100, 1);
                    categories.Item().Column(item =>
                    {
                        item.Item().Row(row =>
                        {
                            row.RelativeItem().Text(category.Name).FontSize(8.5f).SemiBold();
                            row.ConstantItem(72).AlignCenter().Text($"{percentage:0.#}%").FontSize(8).FontColor("#64746A").DirectionFromLeftToRight();
                            row.ConstantItem(105).ContentFromLeftToRight().AlignRight().Text(FormatMoney(category.Total)).FontSize(8.5f).Bold().FontColor("#08783A");
                        });
                        item.Item().PaddingTop(3).Height(5).Row(bar =>
                        {
                            bar.RelativeItem(Math.Max((float)category.Total, 0.01f)).Background("#169B50");
                            bar.RelativeItem(Math.Max((float)(grandTotal - category.Total), 0.01f)).Background("#E3ECE6");
                        });
                    });
                }
            });
        }

        column.Item().Element(container => SectionHeading(container, "تفاصيل المصروفات", "سجل مالي مرتب من الأحدث إلى الأقدم"));
        if (report.Expenses.Count == 0)
        {
            column.Item().Background("#FFF8E9").Border(1).BorderColor("#EFD39A").Padding(13).Column(empty =>
            {
                empty.Item().Text("لا توجد مصروفات مطابقة").FontSize(11).Bold().FontColor("#765018");
                empty.Item().PaddingTop(3).Text("لم تُسجل أي عمليات ضمن الفترة والفئة المحددتين.").FontSize(8.5f).FontColor("#8B6A37");
            });
            return;
        }

        column.Item().Table(table =>
        {
            table.ColumnsDefinition(columns =>
            {
                columns.ConstantColumn(68);
                columns.ConstantColumn(92);
                columns.RelativeColumn(2.6f);
                columns.ConstantColumn(86);
                columns.ConstantColumn(58);
                columns.ConstantColumn(94);
            });
            table.Header(header =>
            {
                header.Cell().Element(TableHeaderCell).AlignCenter().Text("التاريخ");
                header.Cell().Element(TableHeaderCell).Text("الفئة");
                header.Cell().Element(TableHeaderCell).Text("الوصف والمورد");
                header.Cell().Element(TableHeaderCell).Text("طريقة الدفع");
                header.Cell().Element(TableHeaderCell).AlignCenter().Text("المرفقات");
                header.Cell().Element(TableHeaderCell).AlignCenter().Text("المبلغ");
            });

            for (var index = 0; index < report.Expenses.Count; index++)
            {
                var expense = report.Expenses[index];
                var alternate = index % 2 == 1;
                table.Cell().Element(cell => TableBodyCell(cell, alternate)).ContentFromLeftToRight().AlignCenter()
                    .Text(expense.ExpenseDate.ToString("yyyy/MM/dd", CultureInfo.InvariantCulture));
                table.Cell().Element(cell => TableBodyCell(cell, alternate)).Text(expense.ExpenseCategory.Name).ClampLines(2, "...").SemiBold();
                table.Cell().Element(cell => TableBodyCell(cell, alternate)).Column(details =>
                {
                    details.Item().Text(expense.Description).SemiBold();
                    details.Item().PaddingTop(2).Text($"{expense.Supplier ?? "بدون مورد"} - رقم العملية {expense.Id}").FontSize(7.25f).FontColor("#748077");
                });
                table.Cell().Element(cell => TableBodyCell(cell, alternate)).Text(expense.PaymentMethod.ToArabic());
                table.Cell().Element(cell => TableBodyCell(cell, alternate)).AlignCenter()
                    .Text($"{FormatNumber(expense.Attachments.Count)} ملف");
                table.Cell().Element(cell => TableBodyCell(cell, alternate)).ContentFromLeftToRight().AlignCenter()
                    .Text(FormatMoney(expense.Amount)).Bold().FontColor("#08783A");
            }
        });

    }

    private static void SummaryMetric(IContainer container, string label, string value, bool accent = false, bool leftToRight = false)
    {
        container.PaddingHorizontal(8).BorderRight(1).BorderColor("#DCE7DF").Column(column =>
        {
            column.Item().Text(label).FontSize(7.25f).SemiBold().FontColor("#718077");
            var valueContainer = column.Item().PaddingTop(4);
            if (leftToRight)
                valueContainer = valueContainer.ContentFromLeftToRight();
            valueContainer.Text(value).ClampLines(2, "...").FontSize(accent ? 13 : 10).Bold().FontColor(accent ? "#08783A" : "#1D3325");
        });
    }

    private static void SectionHeading(IContainer container, string title, string subtitle) =>
        container.PaddingTop(2).Row(row =>
        {
            row.ConstantItem(4).Height(30).Background("#19A558");
            row.RelativeItem().PaddingRight(8).Column(column =>
            {
                column.Item().Text(title).FontSize(12).Bold().FontColor("#0A2A18");
                column.Item().PaddingTop(1).Text(subtitle).FontSize(7.5f).FontColor("#718077");
            });
        });

    private static IContainer TableHeaderCell(IContainer container) =>
        container.Background("#0B6B38").PaddingVertical(7).PaddingHorizontal(6)
            .DefaultTextStyle(style => style.FontSize(8).SemiBold().FontColor("#FFFFFF"));

    private static IContainer TableBodyCell(IContainer container, bool alternate) =>
        container.ShowEntire().Background(alternate ? "#F7FAF8" : "#FFFFFF").BorderBottom(1).BorderColor("#E2EAE4")
            .PaddingVertical(7).PaddingHorizontal(6).DefaultTextStyle(style => style.FontSize(8));

    private static string FormatMoney(decimal value) => $"{value.ToString("N2", CultureInfo.InvariantCulture)} ج.م";
    private static string FormatNumber(int value) => value.ToString("N0", CultureInfo.InvariantCulture);

    private static void ConfigurePdf()
    {
        if (_pdfConfigured) return;
        lock (PdfConfigurationLock)
        {
            if (_pdfConfigured) return;
            QuestPDF.Settings.License = LicenseType.Community;
            RegisterEmbeddedFont("Greenmind.Api.Assets.Fonts.NotoSansArabic-Regular.ttf");
            RegisterEmbeddedFont("Greenmind.Api.Assets.Fonts.NotoSansArabic-Bold.ttf");
            _pdfConfigured = true;
        }
    }

    private static void RegisterEmbeddedFont(string resourceName)
    {
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException($"Embedded PDF font was not found: {resourceName}");
        FontManager.RegisterFont(stream);
    }

    private const string BrandLogoSvg = """
        <svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 128 128">
          <path fill="#16A34A" d="M103 20c-30-2-55 3-71 17C16 51 10 75 25 95c4-7 11-18 22-30 9-10 20-18 31-23-12 10-25 28-35 59 22 8 45 2 58-16 13-18 12-43 2-65Z"/>
        </svg>
        """;
    private static string Q(string value) => '"' + value.Replace("\"", "\"\"") + '"';
    private sealed record ReportData(
        IReadOnlyList<Greenmind.Domain.Entities.ExportPlan> Plans,
        IReadOnlyList<Greenmind.Domain.Entities.FarmTask> Tasks,
        IReadOnlyList<Greenmind.Domain.Entities.Expense> Expenses,
        string[] Sections,
        string CompanyName,
        DateOnly? From,
        DateOnly? To,
        string? SelectedExpenseCategory,
        DateTime GeneratedAtUtc)
    {
        public bool Has(string value) => Sections.Contains(value, StringComparer.OrdinalIgnoreCase);
        public string Title => Has("spending") && Sections.Length == 1 ? "تقرير المصروفات" : "تقرير إدارة المزرعة";
        public string PlanLabel => Plans.Count == 1 ? Plans[0].Name : $"{Plans.Count.ToString("N0", CultureInfo.InvariantCulture)} خطط";
        public string CategoryLabel => SelectedExpenseCategory ?? "كل الفئات";
        public string PeriodLabel => From.HasValue || To.HasValue
            ? $"{From?.ToString("yyyy/MM/dd", CultureInfo.InvariantCulture) ?? "بداية الخطة"} إلى {To?.ToString("yyyy/MM/dd", CultureInfo.InvariantCulture) ?? "اليوم"}"
            : "الموسم كاملًا";
    }

    private sealed record CategoryTotal(string Name, decimal Total);
}
public sealed record FarmReportRequest(string Format = "pdf", string[]? Sections = null, int? ExportPlanId = null, DateOnly? From = null, DateOnly? To = null, int? ExpenseCategoryId = null);
