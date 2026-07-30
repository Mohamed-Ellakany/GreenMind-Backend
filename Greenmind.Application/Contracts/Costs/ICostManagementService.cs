using Greenmind.Application.Dtos.Costs;

namespace Greenmind.Application.Contracts.Costs;

public interface ICostManagementService
{
    Task<CostOperationResult<IReadOnlyList<ExpenseCategoryDto>>> GetCategoriesAsync(
        string ownerUserId,
        CancellationToken cancellationToken = default);

    Task<CostOperationResult<BudgetDto?>> GetBudgetAsync(
        int exportPlanId,
        string ownerUserId,
        CancellationToken cancellationToken = default);

    Task<CostOperationResult<BudgetDto>> UpsertBudgetAsync(
        int exportPlanId,
        string ownerUserId,
        BudgetRequest request,
        CancellationToken cancellationToken = default);

    Task<CostOperationResult<ExpensePageDto>> ListExpensesAsync(
        int exportPlanId,
        string ownerUserId,
        ExpenseFilters filters,
        CancellationToken cancellationToken = default);

    Task<CostOperationResult<ExpenseDto>> GetExpenseAsync(
        int exportPlanId,
        int expenseId,
        string ownerUserId,
        CancellationToken cancellationToken = default);

    Task<CostOperationResult<ExpenseDto>> CreateExpenseAsync(
        int exportPlanId,
        string ownerUserId,
        ExpenseRequest request,
        CancellationToken cancellationToken = default);

    Task<CostOperationResult<ExpenseDto>> UpdateExpenseAsync(
        int exportPlanId,
        int expenseId,
        string ownerUserId,
        ExpenseRequest request,
        CancellationToken cancellationToken = default);

    Task<CostOperationResult<bool>> DeleteExpenseAsync(
        int exportPlanId,
        int expenseId,
        string ownerUserId,
        CancellationToken cancellationToken = default);

    Task<CostOperationResult<CostDashboardDto>> GetCostDashboardAsync(
        int exportPlanId,
        string ownerUserId,
        CancellationToken cancellationToken = default);

    Task<CostOperationResult<IReadOnlyList<ExportPlanCostComparisonItemDto>>> CompareExportPlansAsync(
        string ownerUserId,
        CancellationToken cancellationToken = default);

    Task<CostOperationResult<ExpenseCsvExport>> ExportExpensesAsync(
        int exportPlanId,
        string ownerUserId,
        ExpenseFilters filters,
        CancellationToken cancellationToken = default);
}

public enum CostOperationStatus
{
    Succeeded,
    NotFound,
    Invalid
}

public sealed record CostOperationResult<T>(
    CostOperationStatus Status,
    T? Value = default,
    string? ErrorCode = null,
    string? ErrorMessage = null)
{
    public static CostOperationResult<T> Success(T value) =>
        new(CostOperationStatus.Succeeded, value);

    public static CostOperationResult<T> NotFound() =>
        new(CostOperationStatus.NotFound);

    public static CostOperationResult<T> Invalid(string code, string message) =>
        new(CostOperationStatus.Invalid, default, code, message);
}
