using Greenmind.Application.Interfaces.AI;
using Microsoft.SemanticKernel;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Text;
using System.Text.Json;

namespace Greenmind.Infrastructure.Agents.Plugins
{
    public class TaskHistoryPlugin
    {
        private readonly ITaskHistoryRepository _repo;
        public TaskHistoryPlugin(ITaskHistoryRepository repo) => _repo = repo;

        [KernelFunction("get_previous_week_tasks")]
        [Description("Gets all planned tasks (with status) for the previous 7 days for this export plan.")]
        public async Task<string> GetPreviousWeekAsync(
            [Description("Export plan id (integer)")] int exportPlanId,
            [Description("Reference date yyyy-MM-dd, previous 7 days before this will be fetched")] string referenceDate)
        {
            var refDate = DateOnly.Parse(referenceDate);
            var tasks = await _repo.GetTasksInRangeAsync(exportPlanId, refDate.AddDays(-7), refDate.AddDays(-1));
            return JsonSerializer.Serialize(tasks);
        }

        [KernelFunction("get_completed_tasks")]
        [Description("Gets tasks actually completed in a date range, to check what was skipped or delayed.")]
        public async Task<string> GetCompletedAsync(
            [Description("Export plan id (integer)")] int exportPlanId,
            [Description("Start date yyyy-MM-dd")] string start,
            [Description("End date yyyy-MM-dd")] string end)
        {
            var tasks = await _repo.GetCompletedTasksAsync(exportPlanId, DateOnly.Parse(start), DateOnly.Parse(end));
            return JsonSerializer.Serialize(tasks);
        }
    }
}
