using System.ComponentModel.DataAnnotations;

namespace Greenmind.Domain.Entities;

public sealed class ContactMessage : BaseEntity
{
    [MaxLength(150)]
    public required string FullName { get; set; }

    [MaxLength(250)]
    public required string Email { get; set; }

    [MaxLength(30)]
    public string? PhoneNumber { get; set; }

    [MaxLength(200)]
    public string? Subject { get; set; }

    [MaxLength(4000)]
    public required string Message { get; set; }

    public bool IsRead { get; set; }

    public DateTime? ReadAtUtc { get; set; }
}
