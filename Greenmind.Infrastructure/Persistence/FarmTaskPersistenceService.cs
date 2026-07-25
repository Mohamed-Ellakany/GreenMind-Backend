using Greenmind.Application.Dtos.AI;
using Greenmind.Application.Interfaces.AI;
using Greenmind.Domain.Entities;
using Greenmind.Infrastructure.Persistence.Mapping;
using Microsoft.EntityFrameworkCore;

namespace Greenmind.Infrastructure.Persistence;

public class FarmTaskPersistenceService : IFarmTaskPersistenceService
{
    private readonly GreenmindDbContext _db;

    public FarmTaskPersistenceService(GreenmindDbContext db) => _db = db;

    public async Task SaveGeneratedTasksAsync(int exportPlanId, AgentResponse agentResponse)
    {
        if (agentResponse.Tasks.Count == 0) return;

        // Load all TaskTypes once, resolve by name in memory (avoids N queries)
        var taskTypes = await _db.TaskTypes.AsNoTracking().ToDictionaryAsync(tt => tt.Name, tt => tt.Id, StringComparer.OrdinalIgnoreCase);

        var entitiesToAdd = new List<FarmTask>();

        foreach (var dto in agentResponse.Tasks)
        {
            if (!taskTypes.TryGetValue(dto.TaskTypeName, out var taskTypeId))
            {
                // Skip (or log) unknown task type names instead of crashing the whole batch
                continue;
            }

            // Avoid duplicating a task of the same type on the same date for this export plan
            var alreadyExists = await _db.FarmTasks.AsNoTracking().AnyAsync(t =>
                t.ExportPlanId == exportPlanId &&
                t.TaskTypeId == taskTypeId &&
                t.PlannedDate == dto.PlannedDate);

            if (alreadyExists) continue;

            entitiesToAdd.Add(new FarmTask
            {
                ExportPlanId = exportPlanId,
                TaskTypeId = taskTypeId,
                Title = dto.Title,
                Description = dto.Description,
                PlannedDate = dto.PlannedDate,
                Status = Greenmind.Domain.Consts.Enums.TaskStatus.Pending,
                Priority = dto.Priority,
                CreatedAt = DateTime.UtcNow
            });
        }

        if (entitiesToAdd.Count > 0)
        {
            _db.FarmTasks.AddRange(entitiesToAdd);
            await _db.SaveChangesAsync();
        }
    }
}