using System.ComponentModel.DataAnnotations;
using Greenmind.Domain.Consts.Enums;

namespace Greenmind.Application.Dtos.Costs;

public sealed record ExpenseCategoryDto(int Id, string Name);

public sealed class BudgetLineRequest
{
    public int ExpenseCategoryId { get; init; }
    public decimal PlannedAmount { get; init; }
}

public sealed class BudgetRequest
{
    public decimal? TotalBudget { get; init; }

    [MaxLength(1000)]
    public string? Notes { get; init; }

    public IReadOnlyList<BudgetLineRequest> Lines { get; init; } = [];
}

public sealed record BudgetLineDto(
    int ExpenseCategoryId,
    string ExpenseCategoryName,
    decimal PlannedAmount);

public sealed record BudgetDto(
    int Id,
    int ExportPlanId,
    decimal? EffectivePlannedBudget,
    decimal? TotalBudget,
    string? Notes,
    IReadOnlyList<BudgetLineDto> Lines,
    DateTime UpdatedAtUtc);

public sealed class ExpenseRequest
{
    public int ExpenseCategoryId { get; init; }
    public int? TaskId { get; init; }
    public DateOnly ExpenseDate { get; init; }

    [Required, MaxLength(500)]
    public string Description { get; init; } = string.Empty;

    [MaxLength(300)]
    public string? Supplier { get; init; }

    public decimal Quantity { get; init; }
    public decimal UnitPrice { get; init; }
    public ExpensePaymentMethod PaymentMethod { get; init; }

    [MaxLength(1000)]
    public string? Notes { get; init; }
}

public sealed class ExpenseFilters
{
    public DateOnly? FromDate { get; init; }
    public DateOnly? ToDate { get; init; }
    public int? ExpenseCategoryId { get; init; }
    public int? TaskId { get; init; }
    public ExpensePaymentMethod? PaymentMethod { get; init; }
    public decimal? MinimumAmount { get; init; }
    public decimal? MaximumAmount { get; init; }
    public string? Search { get; init; }
    public string? Sort { get; init; }
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 25;
}

public sealed record ExpenseDto(
    int Id,
    int ExportPlanId,
    ExpenseCategoryDto ExpenseCategory,
    int? TaskId,
    DateOnly ExpenseDate,
    string Description,
    string? Supplier,
    decimal Quantity,
    decimal UnitPrice,
    decimal Amount,
    string Currency,
    ExpensePaymentMethod PaymentMethod,
    string? Notes,
    DateTime CreatedAt,
    DateTime? UpdatedAtUtc);

public sealed record ExpensePageDto(
    IReadOnlyList<ExpenseDto> Items,
    int Page,
    int PageSize,
    int TotalCount);

public sealed record CostPeriodTotalDto(string Period, decimal Total);

public sealed record CostCategoryTotalDto(
    int ExpenseCategoryId,
    string ExpenseCategoryName,
    decimal Total);

public sealed record CostDashboardDto(
    decimal? PlannedBudget,
    decimal TotalExpenses,
    decimal? RemainingBudget,
    decimal? BudgetConsumptionPercentage,
    decimal? BudgetVariance,
    decimal? CostPerFeddan,
    CostCategoryTotalDto? HighestSpendingCategory,
    IReadOnlyList<CostCategoryTotalDto> ExpensesByCategory,
    IReadOnlyList<CostPeriodTotalDto> ExpensesByMonth,
    IReadOnlyList<CostPeriodTotalDto> ExpensesByIsoWeek,
    IReadOnlyList<ExpenseDto> LatestExpenses);

public sealed record ExportPlanCostComparisonItemDto(
    int ExportPlanId,
    string ExportPlanName,
    decimal? PlannedBudget,
    decimal TotalExpenses,
    decimal? BudgetVariance,
    decimal? CostPerFeddan);

public sealed record ExpenseCsvExport(byte[] Content, string FileName);
