using System.ComponentModel.DataAnnotations;
using Greenmind.Domain.Consts.Enums;

namespace Greenmind.Api.Models;

public class TaskQueryParams
{
    public string? Status { get; set; }
    public int? Priority { get; set; }
    public int? TaskTypeId { get; set; }
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
    public string? Notes { get; set; }
}

public class UpdateTaskRequest
{
    public string? Status { get; set; }
    public DateOnly? ActualDate { get; set; }
    [MaxLength(1000)]
    public string? Notes { get; set; }
}
