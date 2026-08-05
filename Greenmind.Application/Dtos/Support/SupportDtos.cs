using System.ComponentModel.DataAnnotations;
using Greenmind.Domain.Consts.Enums;

namespace Greenmind.Application.Dtos.Support;

public sealed record SupportConversationDto(
    int Id,
    int FarmId,
    string FarmName,
    string OwnerName,
    string? OwnerEmail,
    bool IsClosed,
    DateTime LastMessageAtUtc,
    DateTime? LastAdminReplyAtUtc,
    DateTime? LastCustomerMessageAtUtc,
    string? LastMessagePreview);

public sealed record SupportMessageDto(
    int Id,
    int ConversationId,
    SupportMessageRole Role,
    string SenderUserId,
    string SenderName,
    string Content,
    DateTime CreatedAt);

public sealed record SupportMessagePageDto(
    IReadOnlyList<SupportMessageDto> Items,
    int PageNumber,
    int PageSize,
    int TotalCount)
{
    public int TotalPages => TotalCount == 0
        ? 0
        : (int)Math.Ceiling(TotalCount / (double)PageSize);
}

public sealed record SendSupportMessageRequest(
    [Required, StringLength(4000, MinimumLength = 1)] string Content);
