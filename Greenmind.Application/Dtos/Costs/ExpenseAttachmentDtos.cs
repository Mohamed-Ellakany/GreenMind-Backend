using Greenmind.Domain.Consts.Enums;

namespace Greenmind.Application.Dtos.Costs;

public sealed record ExpenseAttachmentUpload(
    ExpenseAttachmentType AttachmentType,
    IncomingCostFile File);

public sealed record IncomingCostFile(
    string FileName,
    string ClaimedContentType,
    byte[] Content);

public sealed record ExpenseAttachmentDto(
    int Id,
    int ExpenseId,
    ExpenseAttachmentType AttachmentType,
    string OriginalFileName,
    string ContentType,
    long FileSize,
    DateTime CreatedAt);

public sealed record ExpenseAttachmentDownload(
    Stream Content,
    string ContentType,
    string FileName);
