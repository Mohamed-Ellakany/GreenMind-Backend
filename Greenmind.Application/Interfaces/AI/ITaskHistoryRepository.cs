using Greenmind.Application.Dtos.AI;
using System;
using System.Collections.Generic;
using System.Text;

namespace Greenmind.Application.Interfaces.AI
{
    public interface ITaskHistoryRepository
    {
        Task<List<TaskItemDto>> GetTasksInRangeAsync(int exportPlanId, DateOnly start, DateOnly end);
        Task<List<TaskItemDto>> GetCompletedTasksAsync(int exportPlanId, DateOnly start, DateOnly end);

    }
}
