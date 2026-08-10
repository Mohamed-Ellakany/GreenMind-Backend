using Greenmind.Domain.Consts.Enums;
using System.ComponentModel.DataAnnotations;

namespace Greenmind.Domain.Entities;

public class ScheduleRevisionItem : BaseEntity
{
    public int ScheduleRevisionId { get; set; }
    public int? FarmTaskId { get; set; }
    public ScheduleRevisionOperation Operation { get; set; }
    public DateOnly? PreviousPlannedDate { get; set; }
    public DateOnly? NewPlannedDate { get; set; }

    [MaxLength(200)]
    public string TaskTitle { get; set; } = string.Empty;

    [MaxLength(1000)]
    public string? Reason { get; set; }

    public ScheduleRevision ScheduleRevision { get; set; } = null!;
    public FarmTask? FarmTask { get; set; }
}
