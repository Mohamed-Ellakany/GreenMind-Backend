using Greenmind.Domain.Consts.Enums;

namespace Greenmind.Application.Dtos.AI;

public sealed class HistoricalTaskDto
{
    public int ExportPlanId { get; set; }
    public string PlanName { get; set; } = string.Empty;
    public string TaskTypeName { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public DateOnly PlannedDate { get; set; }
    public DateOnly? ActualDate { get; set; }
    public string Status { get; set; } = string.Empty;
    public TaskPriority Priority { get; set; }
    public string? Notes { get; set; }
    public string Origin { get; set; } = string.Empty;
}

public sealed record AvailableTaskTypeDto(string Name, string? Description);
