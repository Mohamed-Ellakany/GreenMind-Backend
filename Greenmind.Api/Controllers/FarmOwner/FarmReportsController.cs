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
        var planRows = await plans.Include(plan => plan.Crop).Include(plan => plan.Destination).ToListAsync(cancellationToken);
        var ids = planRows.Select(plan => plan.Id).ToHashSet();
        var from = request.From?.ToDateTime(TimeOnly.MinValue); var to = request.To?.ToDateTime(TimeOnly.MaxValue);
        var expenses = await db.Expenses.AsNoTracking().Include(item => item.ExpenseCategory).Include(item => item.ExportPlan)
            .Where(item => ids.Contains(item.ExportPlanId) && !item.IsDeleted && (!from.HasValue || item.ExpenseDate >= DateOnly.FromDateTime(from.Value)) && (!to.HasValue || item.ExpenseDate <= DateOnly.FromDateTime(to.Value)))
            .ToListAsync(cancellationToken);
        var tasks = await db.FarmTasks.AsNoTracking().Include(item => item.ExportPlan).Where(item => ids.Contains(item.ExportPlanId) && (!from.HasValue || item.PlannedDate >= DateOnly.FromDateTime(from.Value)) && (!to.HasValue || item.PlannedDate <= DateOnly.FromDateTime(to.Value))).ToListAsync(cancellationToken);
        var selected = request.Sections?.Length > 0
            ? request.Sections.SelectMany(value => value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)).ToArray()
            : ["plans", "tasks", "spending"];
        var report = new ReportData(planRows, tasks, expenses, selected);
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
    private static byte[] ToPdf(ReportData report) => Document.Create(document => document.Page(page => { page.Margin(32); page.DefaultTextStyle(style => style.FontSize(10)); page.Content().Column(column => { column.Item().Text("GreenMind Farm Report").FontSize(20).Bold(); if (report.Has("spending")) { column.Item().PaddingTop(12).Text($"Total spendings: {report.Expenses.Sum(item => item.Amount):N2}").Bold(); foreach (var item in report.Expenses) column.Item().Text($"{item.ExpenseDate:yyyy-MM-dd} | {item.ExportPlan.Name} | {item.ExpenseCategory.Name} | {item.Description} | {item.Amount:N2}"); } if (report.Has("tasks")) { column.Item().PaddingTop(12).Text("Tasks").Bold(); foreach (var task in report.Tasks) column.Item().Text($"{task.PlannedDate:yyyy-MM-dd} | {task.Title} | {task.Status}"); } if (report.Has("plans")) { column.Item().PaddingTop(12).Text("Export plans").Bold(); foreach (var plan in report.Plans) column.Item().Text($"{plan.Name} | {plan.Crop.Name} | {plan.Destination.Name} | {plan.Status}"); } }); })).GeneratePdf();
    private static string Q(string value) => '"' + value.Replace("\"", "\"\"") + '"';
    private sealed record ReportData(IReadOnlyList<Greenmind.Domain.Entities.ExportPlan> Plans, IReadOnlyList<Greenmind.Domain.Entities.FarmTask> Tasks, IReadOnlyList<Greenmind.Domain.Entities.Expense> Expenses, string[] Sections) { public bool Has(string value) => Sections.Contains(value, StringComparer.OrdinalIgnoreCase); }
}
public sealed record FarmReportRequest(string Format = "pdf", string[]? Sections = null, int? ExportPlanId = null, DateOnly? From = null, DateOnly? To = null);
