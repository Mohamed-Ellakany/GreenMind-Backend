using Greenmind.Application.Dtos.AI;
using Greenmind.Application.Interfaces.AI;
using Greenmind.Infrastructure.Persistence.Mapping;
using Microsoft.EntityFrameworkCore;
using DomainTaskStatus = Greenmind.Domain.Consts.Enums.TaskStatus;

namespace Greenmind.Infrastructure.Persistence
{
    public class TaskHistoryRepository : ITaskHistoryRepository
    {
        private readonly GreenmindDbContext _db;
        public TaskHistoryRepository(GreenmindDbContext db) => _db = db;

        public async Task<List<TaskItemDto>> GetTasksInRangeAsync(int exportPlanId, DateOnly start, DateOnly end)
        {
            var entities = await _db.FarmTasks
                .Include(t => t.TaskType)
                .Include(t => t.Activities)
                .Where(t => t.ExportPlanId == exportPlanId
                         && t.PlannedDate >= start
                         && t.PlannedDate <= end)
                .ToListAsync();

            return entities.Select(t => t.ToDto()).ToList();
        }

        public async Task<List<TaskItemDto>> GetCompletedTasksAsync(int exportPlanId, DateOnly start, DateOnly end)
        {
            var entities = await _db.FarmTasks
                .Include(t => t.TaskType)
                .Include(t => t.Activities)
                .Where(t => t.ExportPlanId == exportPlanId
                         && t.Status == DomainTaskStatus.Completed
                         && t.PlannedDate >= start
                         && t.PlannedDate <= end)
                .ToListAsync();

            return entities.Select(t => t.ToDto()).ToList();
        }
    }
}
