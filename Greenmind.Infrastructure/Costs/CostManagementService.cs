using System.Globalization;
using System.Text;
using Greenmind.Application.Contracts.Api;
using Greenmind.Application.Contracts.Costs;
using Greenmind.Application.Dtos.Costs;
using Greenmind.Domain.Consts;
using Greenmind.Domain.Consts.Enums;
using Greenmind.Domain.Entities;
using Greenmind.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Greenmind.Infrastructure.Costs;

public sealed class CostManagementService(
    GreenmindDbContext dbContext,
    TimeProvider timeProvider) : ICostManagementService
{
    public async Task<CostOperationResult<IReadOnlyList<ExpenseCategoryDto>>> GetCategoriesAsync(
        string ownerUserId,
        CancellationToken cancellationToken = default)
    {
        var categories = await dbContext.ExpenseCategories
            .AsNoTracking()
            .Where(category => category.Farm.OwnerUserId == ownerUserId)
            .OrderBy(category => category.Id)
            .Select(category => new ExpenseCategoryDto(category.Id, category.Name))
            .ToListAsync(cancellationToken);

        return CostOperationResult<IReadOnlyList<ExpenseCategoryDto>>.Success(categories);
    }

    public async Task<CostOperationResult<BudgetDto?>> GetBudgetAsync(
        int exportPlanId,
        string ownerUserId,
        CancellationToken cancellationToken = default)
    {
        if (!await OwnsPlanAsync(exportPlanId, ownerUserId, cancellationToken))
            return CostOperationResult<BudgetDto?>.NotFound();

        var budget = await dbContext.ExportPlanBudgets
            .AsNoTracking()
            .Include(item => item.Lines)
                .ThenInclude(line => line.ExpenseCategory)
            .SingleOrDefaultAsync(
                item => item.ExportPlanId == exportPlanId,
                cancellationToken);

        return CostOperationResult<BudgetDto?>.Success(
            budget is null ? null : MapBudget(budget));
    }

    public async Task<CostOperationResult<BudgetDto>> UpsertBudgetAsync(
        int exportPlanId,
        string ownerUserId,
        BudgetRequest request,
        CancellationToken cancellationToken = default)
    {
        var plan = await dbContext.ExportPlans
            .Include(item => item.Farm)
            .SingleOrDefaultAsync(
                item =>
                    item.Id == exportPlanId &&
                    item.Farm.OwnerUserId == ownerUserId,
                cancellationToken);
        if (plan is null)
            return CostOperationResult<BudgetDto>.NotFound();

        var lines = request.Lines ?? [];
        if (request.TotalBudget < 0 ||
            request.Notes?.Length > 1000 ||
            lines.Any(line => line.PlannedAmount < 0) ||
            lines.GroupBy(line => line.ExpenseCategoryId).Any(group => group.Count() > 1))
        {
            return CostOperationResult<BudgetDto>.Invalid(
                ApiErrorCodes.ValidationFailed,
                "بيانات الميزانية غير صالحة.");
        }

        var lineTotal = lines.Sum(line => line.PlannedAmount);
        if (request.TotalBudget.HasValue && lineTotal > request.TotalBudget.Value)
        {
            return CostOperationResult<BudgetDto>.Invalid(
                ApiErrorCodes.BudgetLinesExceedTotal,
                "إجمالي بنود الميزانية يتجاوز الميزانية الكلية.");
        }

        var categoryIds = lines.Select(line => line.ExpenseCategoryId).ToHashSet();
        var ownedCategoryCount = await dbContext.ExpenseCategories
            .CountAsync(
                category =>
                    categoryIds.Contains(category.Id) &&
                    category.FarmId == plan.FarmId,
                cancellationToken);
        if (ownedCategoryCount != categoryIds.Count)
        {
            return CostOperationResult<BudgetDto>.Invalid(
                ApiErrorCodes.ExpenseCategoryInvalid,
                "تحتوي الميزانية على فئة مصروفات غير صالحة.");
        }

        var budget = await dbContext.ExportPlanBudgets
            .Include(item => item.Lines)
            .SingleOrDefaultAsync(
                item => item.ExportPlanId == exportPlanId,
                cancellationToken);
        if (budget is null)
        {
            budget = new ExportPlanBudget
            {
                ExportPlanId = exportPlanId,
                UpdatedAtUtc = timeProvider.GetUtcNow().UtcDateTime
            };
            dbContext.ExportPlanBudgets.Add(budget);
        }

        budget.TotalBudget = request.TotalBudget;
        budget.Notes = Normalize(request.Notes);
        budget.UpdatedAtUtc = timeProvider.GetUtcNow().UtcDateTime;

        var requestedByCategory = lines.ToDictionary(
            line => line.ExpenseCategoryId);
        foreach (var existing in budget.Lines.ToList())
        {
            if (requestedByCategory.Remove(existing.ExpenseCategoryId, out var requested))
                existing.PlannedAmount = requested.PlannedAmount;
            else
                dbContext.ExportPlanBudgetLines.Remove(existing);
        }

        foreach (var requested in requestedByCategory.Values)
        {
            budget.Lines.Add(new ExportPlanBudgetLine
            {
                ExpenseCategoryId = requested.ExpenseCategoryId,
                PlannedAmount = requested.PlannedAmount
            });
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        await dbContext.Entry(budget)
            .Collection(item => item.Lines)
            .Query()
            .Include(line => line.ExpenseCategory)
            .LoadAsync(cancellationToken);

        return CostOperationResult<BudgetDto>.Success(MapBudget(budget));
    }

    public async Task<CostOperationResult<ExpensePageDto>> ListExpensesAsync(
        int exportPlanId,
        string ownerUserId,
        ExpenseFilters filters,
        CancellationToken cancellationToken = default)
    {
        if (!await OwnsPlanAsync(exportPlanId, ownerUserId, cancellationToken))
            return CostOperationResult<ExpensePageDto>.NotFound();

        var page = Math.Max(filters.Page, 1);
        var pageSize = Math.Clamp(filters.PageSize, 1, 100);
        var query = ApplyFilters(
            dbContext.Expenses
                .AsNoTracking()
                .Include(expense => expense.ExpenseCategory)
                .Where(expense => expense.ExportPlanId == exportPlanId),
            filters);

        var totalCount = await query.CountAsync(cancellationToken);
        query = ApplySort(query, filters.Sort);
        var expenses = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return CostOperationResult<ExpensePageDto>.Success(new ExpensePageDto(
            expenses.Select(MapExpense).ToList(),
            page,
            pageSize,
            totalCount));
    }

    public async Task<CostOperationResult<ExpenseDto>> GetExpenseAsync(
        int exportPlanId,
        int expenseId,
        string ownerUserId,
        CancellationToken cancellationToken = default)
    {
        var expense = await LoadOwnedExpenseAsync(
            exportPlanId,
            expenseId,
            ownerUserId,
            tracking: false,
            cancellationToken);
        return expense is null
            ? CostOperationResult<ExpenseDto>.NotFound()
            : CostOperationResult<ExpenseDto>.Success(MapExpense(expense));
    }

    public async Task<CostOperationResult<ExpenseDto>> CreateExpenseAsync(
        int exportPlanId,
        string ownerUserId,
        ExpenseRequest request,
        CancellationToken cancellationToken = default)
    {
        var validation = await ValidateExpenseAsync(
            exportPlanId,
            ownerUserId,
            request,
            cancellationToken);
        if (validation.Error is not null)
            return validation.Error;

        var expense = new Expense
        {
            ExportPlanId = exportPlanId,
            ExpenseCategoryId = request.ExpenseCategoryId,
            TaskId = request.TaskId,
            ExpenseDate = request.ExpenseDate,
            Description = request.Description.Trim(),
            Supplier = Normalize(request.Supplier),
            Quantity = request.Quantity,
            UnitPrice = request.UnitPrice,
            PaymentMethod = request.PaymentMethod,
            Notes = Normalize(request.Notes),
            CreatedByUserId = ownerUserId
        };
        expense.RecalculateAmount();
        dbContext.Expenses.Add(expense);
        await dbContext.SaveChangesAsync(cancellationToken);
        expense.ExpenseCategory = validation.Category!;

        return CostOperationResult<ExpenseDto>.Success(MapExpense(expense));
    }

    public async Task<CostOperationResult<ExpenseDto>> UpdateExpenseAsync(
        int exportPlanId,
        int expenseId,
        string ownerUserId,
        ExpenseRequest request,
        CancellationToken cancellationToken = default)
    {
        var expense = await LoadOwnedExpenseAsync(
            exportPlanId,
            expenseId,
            ownerUserId,
            tracking: true,
            cancellationToken);
        if (expense is null)
            return CostOperationResult<ExpenseDto>.NotFound();

        var validation = await ValidateExpenseAsync(
            exportPlanId,
            ownerUserId,
            request,
            cancellationToken);
        if (validation.Error is not null)
            return validation.Error;

        expense.ExpenseCategoryId = request.ExpenseCategoryId;
        expense.TaskId = request.TaskId;
        expense.ExpenseDate = request.ExpenseDate;
        expense.Description = request.Description.Trim();
        expense.Supplier = Normalize(request.Supplier);
        expense.Quantity = request.Quantity;
        expense.UnitPrice = request.UnitPrice;
        expense.PaymentMethod = request.PaymentMethod;
        expense.Notes = Normalize(request.Notes);
        expense.UpdatedByUserId = ownerUserId;
        expense.UpdatedAtUtc = timeProvider.GetUtcNow().UtcDateTime;
        expense.RecalculateAmount();
        await dbContext.SaveChangesAsync(cancellationToken);
        // The validated category is intentionally assigned after saving. It was
        // loaded without tracking and assigning it earlier can conflict with the
        // category entity already tracked through the expense navigation.
        expense.ExpenseCategory = validation.Category!;

        return CostOperationResult<ExpenseDto>.Success(MapExpense(expense));
    }

    public async Task<CostOperationResult<bool>> DeleteExpenseAsync(
        int exportPlanId,
        int expenseId,
        string ownerUserId,
        CancellationToken cancellationToken = default)
    {
        var expense = await LoadOwnedExpenseAsync(
            exportPlanId,
            expenseId,
            ownerUserId,
            tracking: true,
            cancellationToken);
        if (expense is null)
            return CostOperationResult<bool>.NotFound();

        expense.IsDeleted = true;
        expense.DeletedByUserId = ownerUserId;
        expense.DeletedAtUtc = timeProvider.GetUtcNow().UtcDateTime;
        await dbContext.SaveChangesAsync(cancellationToken);
        return CostOperationResult<bool>.Success(true);
    }

    public async Task<CostOperationResult<CostDashboardDto>> GetCostDashboardAsync(
        int exportPlanId,
        string ownerUserId,
        CancellationToken cancellationToken = default)
    {
        var plan = await dbContext.ExportPlans
            .AsNoTracking()
            .Include(item => item.Budget)
                .ThenInclude(budget => budget!.Lines)
            .Include(item => item.Expenses)
                .ThenInclude(expense => expense.ExpenseCategory)
            .SingleOrDefaultAsync(
                item =>
                    item.Id == exportPlanId &&
                    item.Farm.OwnerUserId == ownerUserId,
                cancellationToken);
        if (plan is null)
            return CostOperationResult<CostDashboardDto>.NotFound();

        var expenses = plan.Expenses.ToList();
        var totalExpenses = expenses.Sum(item => item.Amount);
        var plannedBudget = plan.Budget?.TotalBudget ??
            (plan.Budget?.Lines.Count > 0
                ? plan.Budget.Lines.Sum(line => line.PlannedAmount)
                : null);
        decimal? remaining = plannedBudget.HasValue
            ? Math.Max(plannedBudget.Value - totalExpenses, 0)
            : null;
        decimal? consumption = plannedBudget > 0
            ? decimal.Round(
                totalExpenses / plannedBudget.Value * 100,
                2,
                MidpointRounding.AwayFromZero)
            : null;
        decimal? variance = plannedBudget.HasValue
            ? plannedBudget.Value - totalExpenses
            : null;
        decimal? costPerFeddan = plan.AreaInFeddan > 0
            ? decimal.Round(
                totalExpenses / plan.AreaInFeddan,
                2,
                MidpointRounding.AwayFromZero)
            : null;

        var byCategory = expenses
            .GroupBy(item => new { item.ExpenseCategoryId, item.ExpenseCategory.Name })
            .Select(group => new CostCategoryTotalDto(
                group.Key.ExpenseCategoryId,
                group.Key.Name,
                group.Sum(item => item.Amount)))
            .OrderByDescending(item => item.Total)
            .ThenBy(item => item.ExpenseCategoryName)
            .ToList();
        var byMonth = expenses
            .GroupBy(item => new { item.ExpenseDate.Year, item.ExpenseDate.Month })
            .OrderBy(group => group.Key.Year)
            .ThenBy(group => group.Key.Month)
            .Select(group => new CostPeriodTotalDto(
                $"{group.Key.Year:D4}-{group.Key.Month:D2}",
                group.Sum(item => item.Amount)))
            .ToList();
        var byWeek = expenses
            .GroupBy(item =>
            {
                var date = item.ExpenseDate.ToDateTime(TimeOnly.MinValue);
                return new
                {
                    Year = ISOWeek.GetYear(date),
                    Week = ISOWeek.GetWeekOfYear(date)
                };
            })
            .OrderBy(group => group.Key.Year)
            .ThenBy(group => group.Key.Week)
            .Select(group => new CostPeriodTotalDto(
                $"{group.Key.Year:D4}-W{group.Key.Week:D2}",
                group.Sum(item => item.Amount)))
            .ToList();
        var latest = expenses
            .OrderByDescending(item => item.ExpenseDate)
            .ThenByDescending(item => item.CreatedAt)
            .Take(10)
            .Select(MapExpense)
            .ToList();

        return CostOperationResult<CostDashboardDto>.Success(new CostDashboardDto(
            plannedBudget,
            totalExpenses,
            remaining,
            consumption,
            variance,
            costPerFeddan,
            byCategory.FirstOrDefault(),
            byCategory,
            byMonth,
            byWeek,
            latest));
    }

    public async Task<CostOperationResult<IReadOnlyList<ExportPlanCostComparisonItemDto>>> CompareExportPlansAsync(
        string ownerUserId,
        CancellationToken cancellationToken = default)
    {
        var plans = await dbContext.ExportPlans
            .AsNoTracking()
            .Where(plan => plan.Farm.OwnerUserId == ownerUserId)
            .Include(plan => plan.Budget)
                .ThenInclude(budget => budget!.Lines)
            .Include(plan => plan.Expenses)
            .OrderBy(plan => plan.Name)
            .ToListAsync(cancellationToken);

        var result = plans.Select(plan =>
        {
            var totalExpenses = plan.Expenses.Sum(item => item.Amount);
            var plannedBudget = plan.Budget?.TotalBudget ??
                (plan.Budget?.Lines.Count > 0
                    ? plan.Budget.Lines.Sum(line => line.PlannedAmount)
                    : null);
            return new ExportPlanCostComparisonItemDto(
                plan.Id,
                plan.Name,
                plannedBudget,
                totalExpenses,
                plannedBudget.HasValue
                    ? plannedBudget.Value - totalExpenses
                    : null,
                plan.AreaInFeddan > 0
                    ? decimal.Round(
                        totalExpenses / plan.AreaInFeddan,
                        2,
                        MidpointRounding.AwayFromZero)
                    : null);
        }).ToList();

        return CostOperationResult<IReadOnlyList<ExportPlanCostComparisonItemDto>>.Success(result);
    }

    public async Task<CostOperationResult<ExpenseCsvExport>> ExportExpensesAsync(
        int exportPlanId,
        string ownerUserId,
        ExpenseFilters filters,
        CancellationToken cancellationToken = default)
    {
        if (!await OwnsPlanAsync(exportPlanId, ownerUserId, cancellationToken))
            return CostOperationResult<ExpenseCsvExport>.NotFound();

        var expenses = await ApplySort(
                ApplyFilters(
                    dbContext.Expenses
                        .AsNoTracking()
                        .Include(expense => expense.ExpenseCategory)
                        .Where(expense => expense.ExportPlanId == exportPlanId),
                    filters),
                filters.Sort)
            .ToListAsync(cancellationToken);

        var csv = new StringBuilder();
        csv.AppendLine(
            "التاريخ,الفئة,الوصف,المورد,الكمية,سعر الوحدة,إجمالي المصروف,العملة,طريقة الدفع,الملاحظات");
        foreach (var expense in expenses)
        {
            string[] fields =
            [
                expense.ExpenseDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                expense.ExpenseCategory.Name,
                expense.Description,
                expense.Supplier ?? string.Empty,
                expense.Quantity.ToString("0.000", CultureInfo.InvariantCulture),
                expense.UnitPrice.ToString("0.00", CultureInfo.InvariantCulture),
                expense.Amount.ToString("0.00", CultureInfo.InvariantCulture),
                "EGP",
                expense.PaymentMethod.ToArabic(),
                expense.Notes ?? string.Empty
            ];
            csv.AppendLine(string.Join(',', fields.Select(EscapeCsvField)));
        }

        var preamble = Encoding.UTF8.GetPreamble();
        var content = Encoding.UTF8.GetBytes(csv.ToString());
        var bytes = new byte[preamble.Length + content.Length];
        Buffer.BlockCopy(preamble, 0, bytes, 0, preamble.Length);
        Buffer.BlockCopy(content, 0, bytes, preamble.Length, content.Length);

        return CostOperationResult<ExpenseCsvExport>.Success(new ExpenseCsvExport(
            bytes,
            $"expenses-plan-{exportPlanId}-{DateTime.UtcNow:yyyyMMdd}.csv"));
    }

    private async Task<ExpenseValidation> ValidateExpenseAsync(
        int exportPlanId,
        string ownerUserId,
        ExpenseRequest request,
        CancellationToken cancellationToken)
    {
        var plan = await dbContext.ExportPlans
            .AsNoTracking()
            .SingleOrDefaultAsync(
                item =>
                    item.Id == exportPlanId &&
                    item.Farm.OwnerUserId == ownerUserId,
                cancellationToken);
        if (plan is null)
            return new(null, CostOperationResult<ExpenseDto>.NotFound());

        if (request.ExpenseDate == default ||
            string.IsNullOrWhiteSpace(request.Description) ||
            request.Description.Length > 500 ||
            request.Supplier?.Length > 300 ||
            request.Notes?.Length > 1000 ||
            request.Quantity <= 0 ||
            request.UnitPrice < 0 ||
            !Enum.IsDefined(request.PaymentMethod))
        {
            return new(
                null,
                CostOperationResult<ExpenseDto>.Invalid(
                    ApiErrorCodes.ValidationFailed,
                    "بيانات المصروف غير صالحة."));
        }

        var category = await dbContext.ExpenseCategories
            .AsNoTracking()
            .SingleOrDefaultAsync(
                item =>
                    item.Id == request.ExpenseCategoryId &&
                    item.FarmId == plan.FarmId,
                cancellationToken);
        if (category is null)
        {
            return new(
                null,
                CostOperationResult<ExpenseDto>.Invalid(
                    ApiErrorCodes.ExpenseCategoryInvalid,
                    "فئة المصروفات لا تنتمي إلى هذه المزرعة."));
        }

        if (request.TaskId.HasValue &&
            !await dbContext.FarmTasks.AnyAsync(
                task =>
                    task.Id == request.TaskId.Value &&
                    task.ExportPlanId == exportPlanId,
                cancellationToken))
        {
            return new(
                null,
                CostOperationResult<ExpenseDto>.Invalid(
                    ApiErrorCodes.ExpenseTaskInvalid,
                    "المهمة المحددة لا تنتمي إلى خطة التصدير."));
        }

        return new(category, null);
    }

    private async Task<Expense?> LoadOwnedExpenseAsync(
        int exportPlanId,
        int expenseId,
        string ownerUserId,
        bool tracking,
        CancellationToken cancellationToken)
    {
        var query = dbContext.Expenses
            .Include(expense => expense.ExpenseCategory)
            .Where(expense =>
                expense.Id == expenseId &&
                expense.ExportPlanId == exportPlanId &&
                expense.ExportPlan.Farm.OwnerUserId == ownerUserId);
        if (!tracking)
            query = query.AsNoTracking();

        return await query.SingleOrDefaultAsync(cancellationToken);
    }

    private Task<bool> OwnsPlanAsync(
        int exportPlanId,
        string ownerUserId,
        CancellationToken cancellationToken) =>
        dbContext.ExportPlans.AnyAsync(
            plan =>
                plan.Id == exportPlanId &&
                plan.Farm.OwnerUserId == ownerUserId,
            cancellationToken);

    private static IQueryable<Expense> ApplyFilters(
        IQueryable<Expense> query,
        ExpenseFilters filters)
    {
        if (filters.FromDate.HasValue)
            query = query.Where(item => item.ExpenseDate >= filters.FromDate.Value);
        if (filters.ToDate.HasValue)
            query = query.Where(item => item.ExpenseDate <= filters.ToDate.Value);
        if (filters.ExpenseCategoryId.HasValue)
            query = query.Where(item => item.ExpenseCategoryId == filters.ExpenseCategoryId.Value);
        if (filters.TaskId.HasValue)
            query = query.Where(item => item.TaskId == filters.TaskId.Value);
        if (filters.PaymentMethod.HasValue)
            query = query.Where(item => item.PaymentMethod == filters.PaymentMethod.Value);
        if (filters.MinimumAmount.HasValue)
            query = query.Where(item => item.Amount >= filters.MinimumAmount.Value);
        if (filters.MaximumAmount.HasValue)
            query = query.Where(item => item.Amount <= filters.MaximumAmount.Value);
        if (!string.IsNullOrWhiteSpace(filters.Search))
        {
            var search = filters.Search.Trim();
            query = query.Where(item =>
                item.Description.Contains(search) ||
                (item.Supplier != null && item.Supplier.Contains(search)));
        }

        return query;
    }

    private static IQueryable<Expense> ApplySort(
        IQueryable<Expense> query,
        string? sort) =>
        sort?.Trim().ToLowerInvariant() switch
        {
            "date_asc" => query
                .OrderBy(item => item.ExpenseDate)
                .ThenBy(item => item.CreatedAt),
            "amount_asc" => query
                .OrderBy(item => item.Amount)
                .ThenBy(item => item.Id),
            "amount_desc" => query
                .OrderByDescending(item => item.Amount)
                .ThenByDescending(item => item.Id),
            _ => query
                .OrderByDescending(item => item.ExpenseDate)
                .ThenByDescending(item => item.CreatedAt)
        };

    private static BudgetDto MapBudget(ExportPlanBudget budget)
    {
        var lines = budget.Lines
            .OrderBy(line => line.ExpenseCategoryId)
            .Select(line => new BudgetLineDto(
                line.ExpenseCategoryId,
                line.ExpenseCategory.Name,
                line.PlannedAmount))
            .ToList();
        var effectiveBudget = budget.TotalBudget ??
            (lines.Count == 0 ? null : lines.Sum(line => line.PlannedAmount));

        return new BudgetDto(
            budget.Id,
            budget.ExportPlanId,
            effectiveBudget,
            budget.TotalBudget,
            budget.Notes,
            lines,
            budget.UpdatedAtUtc);
    }

    private static ExpenseDto MapExpense(Expense expense) => new(
        expense.Id,
        expense.ExportPlanId,
        new ExpenseCategoryDto(
            expense.ExpenseCategoryId,
            expense.ExpenseCategory.Name),
        expense.TaskId,
        expense.ExpenseDate,
        expense.Description,
        expense.Supplier,
        expense.Quantity,
        expense.UnitPrice,
        expense.Amount,
        "EGP",
        expense.PaymentMethod,
        expense.Notes,
        expense.CreatedAt,
        expense.UpdatedAtUtc);

    private static string? Normalize(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static string EscapeCsvField(string value)
    {
        if (value.Length > 0 &&
            (value[0] is '=' or '+' or '-' or '@' or '\t' or '\r'))
        {
            value = $"'{value}";
        }

        return value.IndexOfAny([',', '"', '\r', '\n']) >= 0
            ? $"\"{value.Replace("\"", "\"\"")}\""
            : value;
    }

    private sealed record ExpenseValidation(
        ExpenseCategory? Category,
        CostOperationResult<ExpenseDto>? Error);
}
