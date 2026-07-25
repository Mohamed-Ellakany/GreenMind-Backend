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
        public DateOnly PlannedDate { get; set; }
        public TaskPriority Priority { get; set; }
    }
}
