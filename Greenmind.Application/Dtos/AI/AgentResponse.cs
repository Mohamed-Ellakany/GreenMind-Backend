using System;
using System.Collections.Generic;
using System.Text;

namespace Greenmind.Application.Dtos.AI
{
    public class AgentResponse
    {
        public List<TaskItemDto> Tasks { get; set; } = new();

    }
}
