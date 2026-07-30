using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Text;

namespace Greenmind.Domain.Entities
{
    public class Destination : BaseEntity
    {
        [Required]
        [MaxLength(100)]
        public required string Name { get; set; }

        [MaxLength(500)]
        public string? Description { get; set; }

        public bool IsActive { get; set; } = true;

        public bool IsDeleted { get; set; }

        public DateTime? DeletedAtUtc { get; set; }

        public ICollection<ExportPlan> ExportPlans { get; set; } = new List<ExportPlan>();

    }
}
