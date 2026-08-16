using System.ComponentModel.DataAnnotations;

namespace Greenmind.Application.Dtos.Contact;

public sealed record CreateContactMessageRequest(
    [Required(ErrorMessage = "الاسم بالكامل مطلوب"), StringLength(150, MinimumLength = 2, ErrorMessage = "يجب أن يتراوح الاسم بين حرفين و 150 حرفاً")]
    string FullName,

    [Required(ErrorMessage = "البريد الإلكتروني مطلوب"), EmailAddress(ErrorMessage = "البريد الإلكتروني غير صالح"), StringLength(250)]
    string Email,

    [StringLength(30)]
    string? PhoneNumber,

    [StringLength(200)]
    string? Subject,

    [Required(ErrorMessage = "نص الرسالة مطلوب"), StringLength(4000, MinimumLength = 5, ErrorMessage = "يجب أن يحتوي نص الرسالة على 5 أحرف على الأقل")]
    string Message);

public sealed record ContactMessageDto(
    int Id,
    string FullName,
    string Email,
    string? PhoneNumber,
    string? Subject,
    string Message,
    bool IsRead,
    DateTime? ReadAtUtc,
    DateTime CreatedAt);

public sealed record ContactMessagePageDto(
    IReadOnlyList<ContactMessageDto> Items,
    int PageNumber,
    int PageSize,
    int TotalCount,
    int UnreadCount)
{
    public int TotalPages => TotalCount == 0
        ? 0
        : (int)Math.Ceiling(TotalCount / (double)PageSize);
}
