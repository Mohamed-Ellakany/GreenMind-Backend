using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Greenmind.Domain.Entities;

public sealed class ProductMrl : BaseEntity
{
    [Required, MaxLength(200)]
    public required string RegionName { get; set; }

    [Required, MaxLength(200)]
    public required string ProductType { get; set; }

    [Required, MaxLength(50)]
    public required string ExternalPesticideId { get; set; }

    [Required]
    public required string ResidueName { get; set; }

    [Column(TypeName = "decimal(18,6)")]
    public decimal? MaximumResidueLevel { get; set; }

    public bool IsDetectionLimit { get; set; }

    [Required, MaxLength(20)]
    public string Unit { get; set; } = "mg/kg";

    public bool IsDeleted { get; set; }

    public DateTime? DeletedAt { get; set; }
}
