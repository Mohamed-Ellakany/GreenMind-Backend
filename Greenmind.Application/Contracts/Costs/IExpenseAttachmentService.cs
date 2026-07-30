using Greenmind.Application.Dtos.Costs;

namespace Greenmind.Application.Contracts.Costs;

public interface IExpenseAttachmentService
{
    Task<CostOperationResult<ExpenseAttachmentDto>> UploadAsync(
        int exportPlanId,
        int expenseId,
        string ownerUserId,
        ExpenseAttachmentUpload upload,
        CancellationToken cancellationToken = default);

    Task<CostOperationResult<IReadOnlyList<ExpenseAttachmentDto>>> ListAsync(
        int exportPlanId,
        int expenseId,
        string ownerUserId,
        CancellationToken cancellationToken = default);

    Task<CostOperationResult<ExpenseAttachmentDownload>> DownloadAsync(
        int exportPlanId,
        int expenseId,
        int attachmentId,
        string ownerUserId,
        CancellationToken cancellationToken = default);

    Task<CostOperationResult<bool>> DeleteAsync(
        int exportPlanId,
        int expenseId,
        int attachmentId,
        string ownerUserId,
        CancellationToken cancellationToken = default);
}
