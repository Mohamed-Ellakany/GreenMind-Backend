using System.Security.Cryptography;
using Greenmind.Application.Contracts.Api;
using Greenmind.Application.Contracts.Costs;
using Greenmind.Application.Contracts.Storage;
using Greenmind.Application.Dtos.Costs;
using Greenmind.Domain.Entities;
using Greenmind.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Greenmind.Infrastructure.Costs;

public sealed class ExpenseAttachmentService(
    GreenmindDbContext dbContext,
    IPrivateFileStorage fileStorage) : IExpenseAttachmentService
{
    private const int MaximumFileBytes = 10 * 1024 * 1024;

    public async Task<CostOperationResult<ExpenseAttachmentDto>> UploadAsync(
        int exportPlanId,
        int expenseId,
        string ownerUserId,
        ExpenseAttachmentUpload upload,
        CancellationToken cancellationToken = default)
    {
        if (!Enum.IsDefined(upload.AttachmentType))
            return Invalid("تصنيف المرفق غير صالح.");

        var expenseExists = await dbContext.Expenses.AnyAsync(
            expense =>
                expense.Id == expenseId &&
                expense.ExportPlanId == exportPlanId &&
                expense.ExportPlan.Farm.OwnerUserId == ownerUserId,
            cancellationToken);
        if (!expenseExists)
            return CostOperationResult<ExpenseAttachmentDto>.NotFound();

        var originalName = Path.GetFileName(upload.File.FileName);
        var detected = DetectFile(upload.File.Content);
        if (detected is null ||
            upload.File.Content.Length == 0 ||
            upload.File.Content.Length > MaximumFileBytes ||
            string.IsNullOrWhiteSpace(originalName) ||
            originalName.Length > 255 ||
            !ContentTypeMatches(upload.File.ClaimedContentType, detected.Value.ContentType))
        {
            return Invalid("يجب رفع ملف PDF أو JPEG أو PNG صالح ضمن الحد المسموح.");
        }

        string? storageKey = null;
        try
        {
            await using var content = new MemoryStream(
                upload.File.Content,
                writable: false);
            storageKey = await fileStorage.SaveAsync(
                "expenses",
                detected.Value.Extension,
                content,
                cancellationToken);
            var attachment = new ExpenseAttachment
            {
                ExpenseId = expenseId,
                AttachmentType = upload.AttachmentType,
                OriginalFileName = originalName,
                StorageKey = storageKey,
                ContentType = detected.Value.ContentType,
                FileSize = upload.File.Content.Length,
                Checksum = Convert.ToHexString(
                    SHA256.HashData(upload.File.Content)),
                UploadedByUserId = ownerUserId
            };
            dbContext.ExpenseAttachments.Add(attachment);
            await dbContext.SaveChangesAsync(cancellationToken);
            return CostOperationResult<ExpenseAttachmentDto>.Success(Map(attachment));
        }
        catch
        {
            if (storageKey is not null)
            {
                try
                {
                    await fileStorage.DeleteAsync(storageKey, CancellationToken.None);
                }
                catch
                {
                    // Preserve the original failure.
                }
            }
            throw;
        }
    }

    public async Task<CostOperationResult<IReadOnlyList<ExpenseAttachmentDto>>> ListAsync(
        int exportPlanId,
        int expenseId,
        string ownerUserId,
        CancellationToken cancellationToken = default)
    {
        if (!await OwnsActiveExpenseAsync(
                exportPlanId,
                expenseId,
                ownerUserId,
                cancellationToken))
        {
            return CostOperationResult<IReadOnlyList<ExpenseAttachmentDto>>.NotFound();
        }

        var attachments = await dbContext.ExpenseAttachments
            .AsNoTracking()
            .Where(item => item.ExpenseId == expenseId)
            .OrderByDescending(item => item.CreatedAt)
            .ToListAsync(cancellationToken);
        return CostOperationResult<IReadOnlyList<ExpenseAttachmentDto>>.Success(
            attachments.Select(Map).ToList());
    }

    public async Task<CostOperationResult<ExpenseAttachmentDownload>> DownloadAsync(
        int exportPlanId,
        int expenseId,
        int attachmentId,
        string ownerUserId,
        CancellationToken cancellationToken = default)
    {
        var attachment = await LoadOwnedAsync(
            exportPlanId,
            expenseId,
            attachmentId,
            ownerUserId,
            cancellationToken);
        if (attachment is null)
            return CostOperationResult<ExpenseAttachmentDownload>.NotFound();

        var stream = await fileStorage.OpenReadAsync(
            attachment.StorageKey,
            cancellationToken);
        return CostOperationResult<ExpenseAttachmentDownload>.Success(
            new ExpenseAttachmentDownload(
                stream,
                attachment.ContentType,
                attachment.OriginalFileName));
    }

    public async Task<CostOperationResult<bool>> DeleteAsync(
        int exportPlanId,
        int expenseId,
        int attachmentId,
        string ownerUserId,
        CancellationToken cancellationToken = default)
    {
        var attachment = await LoadOwnedAsync(
            exportPlanId,
            expenseId,
            attachmentId,
            ownerUserId,
            cancellationToken);
        if (attachment is null)
            return CostOperationResult<bool>.NotFound();

        await fileStorage.DeleteAsync(attachment.StorageKey, cancellationToken);
        dbContext.ExpenseAttachments.Remove(attachment);
        await dbContext.SaveChangesAsync(cancellationToken);
        return CostOperationResult<bool>.Success(true);
    }

    private Task<bool> OwnsActiveExpenseAsync(
        int exportPlanId,
        int expenseId,
        string ownerUserId,
        CancellationToken cancellationToken) =>
        dbContext.Expenses.AnyAsync(
            expense =>
                expense.Id == expenseId &&
                expense.ExportPlanId == exportPlanId &&
                expense.ExportPlan.Farm.OwnerUserId == ownerUserId,
            cancellationToken);

    private Task<ExpenseAttachment?> LoadOwnedAsync(
        int exportPlanId,
        int expenseId,
        int attachmentId,
        string ownerUserId,
        CancellationToken cancellationToken) =>
        dbContext.ExpenseAttachments.SingleOrDefaultAsync(
            item =>
                item.Id == attachmentId &&
                item.ExpenseId == expenseId &&
                item.Expense.ExportPlanId == exportPlanId &&
                item.Expense.ExportPlan.Farm.OwnerUserId == ownerUserId,
            cancellationToken);

    private static (string ContentType, string Extension)? DetectFile(byte[] content)
    {
        if (content.Length >= 5 &&
            content.AsSpan(0, 5).SequenceEqual("%PDF-"u8))
            return ("application/pdf", ".pdf");
        if (content.Length >= 8 &&
            content.AsSpan(0, 8).SequenceEqual(
                new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A }))
            return ("image/png", ".png");
        if (content.Length >= 3 &&
            content[0] == 0xFF && content[1] == 0xD8 && content[2] == 0xFF)
            return ("image/jpeg", ".jpg");
        return null;
    }

    private static bool ContentTypeMatches(string claimed, string detected) =>
        string.Equals(claimed.Trim(), detected, StringComparison.OrdinalIgnoreCase) ||
        (detected == "image/jpeg" &&
         string.Equals(claimed.Trim(), "image/jpg", StringComparison.OrdinalIgnoreCase));

    private static CostOperationResult<ExpenseAttachmentDto> Invalid(string message) =>
        CostOperationResult<ExpenseAttachmentDto>.Invalid(
            ApiErrorCodes.AttachmentInvalid,
            message);

    private static ExpenseAttachmentDto Map(ExpenseAttachment attachment) => new(
        attachment.Id,
        attachment.ExpenseId,
        attachment.AttachmentType,
        attachment.OriginalFileName,
        attachment.ContentType,
        attachment.FileSize,
        attachment.CreatedAt);
}
