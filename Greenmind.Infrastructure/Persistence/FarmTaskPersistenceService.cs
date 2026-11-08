using Greenmind.Application.Dtos.AI;
using Greenmind.Application.Interfaces.AI;
using Greenmind.Domain.Consts;
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

        // Load all TaskTypes once, resolve by normalized name in memory
        var rawTaskTypes = await _db.TaskTypes.AsNoTracking().ToListAsync();
        var taskTypes = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var tt in rawTaskTypes)
        {
            taskTypes[tt.Name] = tt.Id;
            var normalized = TaskTypeNames.Normalize(tt.Name);
            if (!string.IsNullOrWhiteSpace(normalized))
                taskTypes[normalized] = tt.Id;
        }

        var entitiesToAdd = new List<FarmTask>();

        foreach (var dto in agentResponse.Tasks)
        {
            var normalizedName = TaskTypeNames.Normalize(dto.TaskTypeName);
            var resolvedUnit = string.IsNullOrWhiteSpace(dto.Unit) ? "وحدة" : dto.Unit;
            var resolvedQuantity = dto.Quantity > 0 ? dto.Quantity : 1;

            if (!taskTypes.TryGetValue(normalizedName, out var taskTypeId) &&
                !taskTypes.TryGetValue(dto.TaskTypeName, out taskTypeId))
            {
                // Skip unknown task type names instead of crashing the whole batch
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
                Quantity = dto.Quantity,
                Unit = dto.Unit,
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
