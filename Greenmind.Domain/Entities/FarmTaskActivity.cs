using Greenmind.Domain.Consts.Enums;
using System.ComponentModel.DataAnnotations;
using DomainTaskStatus = Greenmind.Domain.Consts.Enums.TaskStatus;

namespace Greenmind.Domain.Entities;

public class FarmTaskActivity : BaseEntity
{
    [Required]
    public int FarmTaskId { get; set; }

    [MaxLength(80)]
    public string EventType { get; set; } = "Updated";

    [MaxLength(50)]
    public string Source { get; set; } = "User";

    [MaxLength(450)]
    public string? ActorUserId { get; set; }

    [MaxLength(200)]
    public string? ActorName { get; set; }

    public DomainTaskStatus? PreviousStatus { get; set; }
    public DomainTaskStatus? NewStatus { get; set; }
    public DateOnly? PreviousPlannedDate { get; set; }
    public DateOnly? NewPlannedDate { get; set; }
    public DateOnly? ActualDate { get; set; }

    [MaxLength(1000)]
    public string? Note { get; set; }

    public FarmTask FarmTask { get; set; } = null!;
}
