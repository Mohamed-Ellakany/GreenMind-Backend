using System.Globalization;
using System.Security.Claims;
using System.Text;
using ClosedXML.Excel;
using Greenmind.Domain.Consts;
using Greenmind.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace Greenmind.Api.Controllers.FarmOwner;

[ApiController]
[Route("api/farm-owner/reports")]
[Authorize(Roles = RoleNames.FarmOwner)]
public sealed class FarmReportsController(GreenmindDbContext db) : ControllerBase
{
    [HttpGet("export")]
    public async Task<IActionResult> Export([FromQuery] FarmReportRequest request, CancellationToken cancellationToken)
    {
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
        var from = request.From?.ToDateTime(TimeOnly.MinValue); var to = request.To?.ToDateTime(TimeOnly.MaxValue);
        var expenseQuery = db.Expenses.AsNoTracking().Include(item => item.ExpenseCategory).Include(item => item.ExportPlan)
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
            request.To);
        var name = $"greenmind-report-{DateTime.UtcNow:yyyyMMdd}";
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
            page.Size(PageSizes.A4);
            page.MarginHorizontal(34);
            page.MarginVertical(28);
            page.DefaultTextStyle(style => style.FontSize(9).FontColor("#25372C"));

            page.Header().PaddingBottom(16).Row(row =>
            {
                row.RelativeItem().Column(column =>
                {
                    column.Item().Text(report.CompanyName).FontSize(10).SemiBold().FontColor("#168148");
                    column.Item().PaddingTop(3).Text("Financial Expense Report").FontSize(22).Bold().FontColor("#0A2A18");
                    column.Item().PaddingTop(4).Text($"Export plan: {report.PlanLabel}").FontSize(9).FontColor("#65756B");
                });
                row.ConstantItem(58).Height(58).Background("#E8F6EC").Padding(10).Svg(BrandLogoSvg);
            });

            page.Content().Column(column =>
            {
                column.Spacing(13);

                column.Item().Background("#F3F8F4").Border(1).BorderColor("#DCE8DF").Padding(12).Row(row =>
                {
                    row.RelativeItem().Element(container => SummaryMetric(container, "REPORT PERIOD", report.PeriodLabel));
                    row.RelativeItem().Element(container => SummaryMetric(container, "EXPENSES", report.Expenses.Count.ToString("N0", CultureInfo.InvariantCulture)));
                    row.RelativeItem().Element(container => SummaryMetric(container, "TOTAL SPENDING", $"{report.Expenses.Sum(item => item.Amount):N2} EGP", true));
                });

                if (report.Has("spending"))
                {
                    column.Item().Text("Spending by category").FontSize(13).Bold().FontColor("#0A2A18");
                    var categoryTotals = report.Expenses
                        .GroupBy(item => item.ExpenseCategory.Name)
                        .Select(group => new { Name = group.Key, Total = group.Sum(item => item.Amount) })
                        .OrderByDescending(item => item.Total)
                        .ToArray();
                    column.Item().Row(row =>
                    {
                        foreach (var category in categoryTotals.Take(4))
                        {
                            row.RelativeItem().PaddingRight(6).Background("#FBFDFC").Border(1).BorderColor("#E3EBE5").Padding(8).Column(card =>
                            {
                                card.Item().Text(category.Name).FontSize(8).FontColor("#65756B");
                                card.Item().PaddingTop(3).Text($"{category.Total:N2} EGP").FontSize(10).Bold().FontColor("#08783A");
                            });
                        }
                    });

                    column.Item().Text("Expense details").FontSize(13).Bold().FontColor("#0A2A18");
                    column.Item().Table(table =>
                    {
                        table.ColumnsDefinition(columns =>
                        {
                            columns.ConstantColumn(64);
                            columns.RelativeColumn(1.1f);
                            columns.RelativeColumn(1.7f);
                            columns.RelativeColumn(1.2f);
                            columns.ConstantColumn(82);
                        });
                        table.Header(header =>
                        {
                            header.Cell().Element(TableHeaderCell).Text("Date");
                            header.Cell().Element(TableHeaderCell).Text("Category");
                            header.Cell().Element(TableHeaderCell).Text("Description");
                            header.Cell().Element(TableHeaderCell).Text("Supplier");
                            header.Cell().Element(TableHeaderCell).AlignRight().Text("Amount (EGP)");
                        });
                        foreach (var item in report.Expenses)
                        {
                            table.Cell().Element(TableBodyCell).Text(item.ExpenseDate.ToString("yyyy-MM-dd"));
                            table.Cell().Element(TableBodyCell).Text(item.ExpenseCategory.Name);
                            table.Cell().Element(TableBodyCell).Text(item.Description);
                            table.Cell().Element(TableBodyCell).Text(item.Supplier ?? "-");
                            table.Cell().Element(TableBodyCell).AlignRight().Text(item.Amount.ToString("N2"));
                        }
                    });

                    if (report.Expenses.Count == 0)
                        column.Item().Background("#FFF8E9").Border(1).BorderColor("#F1D69E").Padding(12).Text("No expenses match the selected report filters.").FontColor("#7A4E12");
                }

                if (report.Has("tasks"))
                {
                    column.Item().Text("Tasks").FontSize(13).Bold().FontColor("#0A2A18");
                    foreach (var task in report.Tasks)
                        column.Item().Text($"{task.PlannedDate:yyyy-MM-dd}  •  {task.Title}  •  {task.Status}");
                }

                if (report.Has("plans"))
                {
                    column.Item().Text("Export plans").FontSize(13).Bold().FontColor("#0A2A18");
                    foreach (var plan in report.Plans)
                        column.Item().Text($"{plan.Name}  •  {plan.Crop.Name}  •  {plan.Destination.Name}  •  {plan.Status}");
                }
            });

            page.Footer().PaddingTop(10).BorderTop(1).BorderColor("#DCE8DF").Row(row =>
            {
                row.RelativeItem().Text($"Generated by GreenMind • {DateTime.UtcNow:yyyy-MM-dd HH:mm} UTC").FontSize(8).FontColor("#75847B");
                row.AutoItem().Text(text =>
                {
                    text.DefaultTextStyle(style => style.FontSize(8).FontColor("#75847B"));
                    text.Span("Page ");
                    text.CurrentPageNumber();
                    text.Span(" of ");
                    text.TotalPages();
                });
            });
        });
    }).GeneratePdf();

    private static void SummaryMetric(IContainer container, string label, string value, bool accent = false) =>
        container.PaddingHorizontal(6).Column(column =>
        {
            column.Item().Text(label).FontSize(7).SemiBold().FontColor("#75847B");
            column.Item().PaddingTop(4).Text(value).FontSize(accent ? 13 : 10).Bold().FontColor(accent ? "#08783A" : "#1E3326");
        });

    private static IContainer TableHeaderCell(IContainer container) =>
        container.Background("#0B6B38").PaddingVertical(7).PaddingHorizontal(6).DefaultTextStyle(style => style.FontSize(8).SemiBold().FontColor("#FFFFFF"));

    private static IContainer TableBodyCell(IContainer container) =>
        container.BorderBottom(1).BorderColor("#E5ECE7").PaddingVertical(7).PaddingHorizontal(6);

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
        DateOnly? To)
    {
        public bool Has(string value) => Sections.Contains(value, StringComparer.OrdinalIgnoreCase);
        public string PlanLabel => Plans.Count == 1 ? Plans[0].Name : $"{Plans.Count} plans";
        public string PeriodLabel => From.HasValue || To.HasValue
            ? $"{From?.ToString("yyyy-MM-dd") ?? "Start"} to {To?.ToString("yyyy-MM-dd") ?? "Today"}"
            : "Full season";
    }
}
public sealed record FarmReportRequest(string Format = "pdf", string[]? Sections = null, int? ExportPlanId = null, DateOnly? From = null, DateOnly? To = null, int? ExpenseCategoryId = null);
