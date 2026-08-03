using System.ComponentModel.DataAnnotations;

namespace Greenmind.Domain.Entities;

public sealed class AdminInvitation : BaseEntity
{
    [Required, MaxLength(150)] public required string FullName { get; set; }
    [Required, MaxLength(256)] public required string Email { get; set; }
    [Required, MaxLength(128)] public required string TokenHash { get; set; }
    [Required, MaxLength(450)] public required string CreatedByUserId { get; set; }
    public DateTime ExpiresAtUtc { get; set; }
    public DateTime? AcceptedAtUtc { get; set; }
    public DateTime? RevokedAtUtc { get; set; }
    public ApplicationUser CreatedByUser { get; set; } = null!;
}
