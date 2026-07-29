using System.ComponentModel.DataAnnotations;
using Greenmind.Domain.Consts.Enums;

namespace Greenmind.Domain.Entities;

public sealed class ActiveSubstance : BaseEntity
{
    [Required, MaxLength(200)]
    public required string RegionName { get; set; }

    public int ExternalId { get; set; }

    [Required, MaxLength(500)]
    public required string Name { get; set; }

    [MaxLength(100)]
    public string? CasNumber { get; set; }

    public ActiveSubstanceApprovalStatus ApprovalStatus { get; set; }

    public DateOnly? ApprovalExpiresOn { get; set; }

    [MaxLength(1000)]
    public string? Legislation { get; set; }

    public string? Remark { get; set; }

    [MaxLength(1000)]
    public string? RegulationReference { get; set; }

    [MaxLength(1000)]
    public string? Functions { get; set; }

    public bool IsDeleted { get; set; }

    public DateTime? DeletedAt { get; set; }
}
