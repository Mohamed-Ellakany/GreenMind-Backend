using System.ComponentModel.DataAnnotations;
using Greenmind.Domain.Consts.Enums;
using DomainTaskStatus = Greenmind.Domain.Consts.Enums.TaskStatus;

namespace Greenmind.Api.Models;

public class TaskQueryParams
{
    public int? ExportPlanId { get; set; }
    public string? Status { get; set; }
    public int? Priority { get; set; }
    public int? TaskTypeId { get; set; }
    public string? Origin { get; set; }
    public string? Search { get; set; }
    public DateOnly? PlannedDateFrom { get; set; }
    public DateOnly? PlannedDateTo { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 50;
}

public class TaskDto
{
    public int Id { get; set; }
    public int ExportPlanId { get; set; }
    public int TaskTypeId { get; set; }
    public string TaskTypeName { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public DateOnly PlannedDate { get; set; }
    public DateOnly? ActualDate { get; set; }
    public Greenmind.Domain.Consts.Enums.TaskStatus Status { get; set; }
    public TaskPriority Priority { get; set; }
    public TaskOrigin Origin { get; set; }
    public string? Notes { get; set; }
    public IReadOnlyList<TaskActivityDto>? Activities { get; set; }
}

public class UpdateTaskRequest
{
    public string? Status { get; set; }
    public DateOnly? PlannedDate { get; set; }
    public DateOnly? ActualDate { get; set; }
    [MaxLength(1000)]
    public string? Notes { get; set; }
}

public class CreateTaskRequest
{
    [Required]
    public int ExportPlanId { get; set; }

    [Required]
    public int TaskTypeId { get; set; }

    [Required, MaxLength(200)]
    public string Title { get; set; } = string.Empty;

    [MaxLength(1000)]
    public string? Description { get; set; }

    [Range(typeof(decimal), "0.001", "100000000")]
    public decimal Quantity { get; set; } = 1;

    [Required, MaxLength(50)]
    public string Unit { get; set; } = "وحدة";

    [Required]
    public DateOnly PlannedDate { get; set; }

    public DateOnly? ActualDate { get; set; }
    public string Status { get; set; } = "Pending";
    public TaskPriority Priority { get; set; } = TaskPriority.Medium;

    [MaxLength(1000)]
    public string? Notes { get; set; }
}

public sealed class TaskActivityDto
{
    public int Id { get; init; }
    public string EventType { get; init; } = string.Empty;
    public string Source { get; init; } = string.Empty;
    public string? ActorName { get; init; }
    public DomainTaskStatus? PreviousStatus { get; init; }
    public DomainTaskStatus? NewStatus { get; init; }
    public DateOnly? PreviousPlannedDate { get; init; }
    public DateOnly? NewPlannedDate { get; init; }
    public DateOnly? ActualDate { get; init; }
    public string? Note { get; init; }
    public DateTime CreatedAt { get; init; }
}

public sealed class TaskTypeDto
{
    public int Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public string? Description { get; init; }
}
