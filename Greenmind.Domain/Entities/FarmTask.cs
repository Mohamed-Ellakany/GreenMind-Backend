

using Greenmind.Domain.Consts.Enums;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Greenmind.Domain.Entities
{
    public class FarmTask : BaseEntity
    {
        [Required]
        [ForeignKey(nameof(ExportPlan))]
        public int ExportPlanId { get; set; }

        [Required]
        [ForeignKey(nameof(TaskType))]
        public int TaskTypeId { get; set; }

        [Required]
        [MaxLength(200)]
        public required string Title { get; set; }

        [MaxLength(1000)]
        public string? Description { get; set; }

        public DateOnly PlannedDate { get; set; }

        public DateOnly? ActualDate { get; set; }

        [Required]
        public Greenmind.Domain.Consts.Enums.TaskStatus Status { get; set; }

        [Required]
        public TaskPriority Priority { get; set; }

        [MaxLength(1000)]
        public string? Notes { get; set; }

        // Navigation
        public ExportPlan ExportPlan { get; set; } = null!;
        public TaskType TaskType { get; set; } = null!;
    }
}
