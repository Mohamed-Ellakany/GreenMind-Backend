using Greenmind.Domain.Consts.Enums;
using System;
using System.Collections.Generic;
using System.Text;

namespace Greenmind.Application.Dtos.AI
{
    public class TaskItemDto
    {
        public string TaskTypeName { get; set; }
        public string Title { get; set; }
        public string Description { get; set; }
        public decimal Quantity { get; set; }
        public string Unit { get; set; }
        public DateOnly PlannedDate { get; set; }
        public DateOnly? ActualDate { get; set; }
        public string Status { get; set; } = string.Empty;
        public TaskPriority Priority { get; set; }
        public string? Notes { get; set; }
        public string Origin { get; set; } = string.Empty;
        public List<TaskActivityItemDto> Activities { get; set; } = new();
    }

    public class TaskActivityItemDto
    {
        public string EventType { get; set; } = string.Empty;
        public string Source { get; set; } = string.Empty;
        public string? Note { get; set; }
        public string? PreviousStatus { get; set; }
        public string? NewStatus { get; set; }
        public DateOnly? PreviousPlannedDate { get; set; }
        public DateOnly? NewPlannedDate { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}
