using System.ComponentModel.DataAnnotations;
using Greenmind.Domain.Consts.Enums;

namespace Greenmind.Application.Dtos.Chat;

public sealed class CreateChatSessionRequest
{
    [Required, StringLength(200, MinimumLength = 1)]
    public string Title { get; init; } = string.Empty;
}

public sealed class SendChatMessageRequest
{
    [StringLength(4000)]
    public string Content { get; init; } = string.Empty;

    [Required, StringLength(100, MinimumLength = 8)]
    public string IdempotencyKey { get; init; } = string.Empty;

    public IReadOnlyList<ChatImageInputDto> Images { get; init; } = [];
}

public sealed record ChatImageInputDto(string FileName, string ContentType, string DataBase64);

public sealed record ChatAttachmentDto(
    int Id,
    int ChatMessageId,
    string OriginalFileName,
    string ContentType,
    long FileSize);

public sealed record ChatAttachmentDownload(
    Stream Content,
    string ContentType,
    string FileName);

public sealed record ChatSessionDto(
    int Id,
    int FarmId,
    int? ExportPlanId,
    ChatScope Scope,
    string Title,
    bool IsActive,
    DateTime CreatedAt,
    DateTime? UpdatedAtUtc);

public sealed record ChatMessageDto(
    int Id,
    ChatMessageRole Role,
    string Content,
    ChatMessageStatus Status,
    DateTime CreatedAt,
    int? TaskChangeProposalId = null,
    IReadOnlyList<ChatAttachmentDto>? Attachments = null);

public sealed record ChatHistoryPageDto(
    IReadOnlyList<ChatMessageDto> Items,
    int PageNumber,
    int PageSize,
    int TotalCount)
{
    public int TotalPages => TotalCount == 0
        ? 0
        : (int)Math.Ceiling(TotalCount / (double)PageSize);
}

public sealed record ChatStreamEventDto(
    string Event,
    ChatMessageStatus Status,
    string? Delta = null,
    int? MessageId = null,
    string? Content = null,
    int? TaskChangeProposalId = null,
    bool Retryable = false);
