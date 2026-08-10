using Greenmind.Domain.Consts.Enums;
using System.ComponentModel.DataAnnotations;

namespace Greenmind.Domain.Entities;

public class ScheduleRevision : BaseEntity
{
    public int ExportPlanId { get; set; }

    [Required]
    public ScheduleRevisionStatus Status { get; set; } = ScheduleRevisionStatus.Pending;

    [MaxLength(80)]
    public string Source { get; set; } = "TaskUpdate";

    public DateTime? StartedAtUtc { get; set; }
    public DateTime? CompletedAtUtc { get; set; }
    public int AttemptCount { get; set; }

    [MaxLength(1000)]
    public string? FailureMessage { get; set; }

    public ExportPlan ExportPlan { get; set; } = null!;
    public ICollection<ScheduleRevisionItem> Items { get; set; } = new List<ScheduleRevisionItem>();
}
