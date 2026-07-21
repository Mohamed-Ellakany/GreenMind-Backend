using System;
using System.Collections.Generic;
using System.Text;

namespace Greenmind.Application.Dtos.AI
{
    public class TaskContextDto
    {
        public List<TaskItemDto> ExistingTasks { get; set; } = new();
        public List<TaskItemDto> CompletedTasks { get; set; } = new();
        public List<string> CurrentTaskStatuses { get; set; } = new();
    }
}
