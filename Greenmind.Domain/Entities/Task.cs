

using Greenmind.Domain.Consts.Enums;

namespace Greenmind.Domain.Entities
{
    public class Task : BaseEntity
    {
        public int ExportPlanId { get; set; }

        public int TaskTypeId { get; set; }

        public required string Title { get; set; }

        public string? Description { get; set; }

        public DateOnly PlannedDate { get; set; }

        public DateOnly? ActualDate { get; set; }

        public Greenmind.Domain.Consts.Enums.TaskStatus Status { get; set; }

        public TaskPriority Priority { get; set; }

        public string? Notes { get; set; }

        // Navigation

        public ExportPlan ExportPlan { get; set; } = null!;

        public TaskType TaskType { get; set; } = null!;
    }
}
