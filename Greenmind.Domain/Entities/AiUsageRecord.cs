using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Greenmind.Domain.Entities;

public sealed class AiUsageRecord : BaseEntity
{
    [Required, MaxLength(100)]
    public required string ProviderRequestId { get; set; }

    [MaxLength(450)]
    public string? UserId { get; set; }

    public int? FarmId { get; set; }

    public int? ExportPlanId { get; set; }

    [Required, MaxLength(80)]
    public required string Feature { get; set; }

    [Required, MaxLength(150)]
    public required string ModelId { get; set; }

    [MaxLength(50)]
    public string? Region { get; set; }

    public int InputTokens { get; set; }

    public int OutputTokens { get; set; }

    public int TotalTokens { get; set; }

    [MaxLength(50)]
    public string? StopReason { get; set; }

    [MaxLength(50)]
    public string? BudgetState { get; set; }

    public bool FallbackUsed { get; set; }

    [Column(TypeName = "decimal(18,8)")]
    public decimal EstimatedCostUsd { get; set; }

    [Column(TypeName = "decimal(18,8)")]
    public decimal ActualCostUsd { get; set; }

    [Required, MaxLength(40)]
    public required string ProviderStatus { get; set; }

    public DateTime CompletedAtUtc { get; set; }

    public ApplicationUser? User { get; set; }

    public Farm? Farm { get; set; }

    public ExportPlan? ExportPlan { get; set; }
}
