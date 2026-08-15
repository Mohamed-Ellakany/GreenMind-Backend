using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Greenmind.Domain.Consts.Enums;

namespace Greenmind.Domain.Entities;

public sealed class AiLimitRequest : BaseEntity
{
    [Required, MaxLength(450)]
    public required string UserId { get; set; }

    [Required, MaxLength(200)]
    public required string ContactName { get; set; }

    [Required, MaxLength(30)]
    public required string ContactPhone { get; set; }

    public DateTime RequestedAtUtc { get; set; }

    public AiLimitRequestStatus Status { get; set; } = AiLimitRequestStatus.Pending;

    [MaxLength(500)]
    public string? AdminNote { get; set; }

    [Column(TypeName = "decimal(18,8)")]
    public decimal? NewLimitUsd { get; set; }

    public DateTime? ResolvedAtUtc { get; set; }

    [MaxLength(450)]
    public string? ResolvedByUserId { get; set; }

    public ApplicationUser User { get; set; } = null!;

    public ApplicationUser? ResolvedByUser { get; set; }
}
