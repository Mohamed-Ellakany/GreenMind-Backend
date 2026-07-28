using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Greenmind.Infrastructure.Persistence;
using Greenmind.Api.Models;
using Greenmind.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using Greenmind.Domain.Consts.Enums;
using Greenmind.Domain.Consts;

namespace Greenmind.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = RoleNames.FarmOwner + "," + RoleNames.Admin)]
public class TasksController : ControllerBase
{
    private readonly GreenmindDbContext _db;

    public TasksController(GreenmindDbContext db)
    {
        _db = db;
    }

    [HttpGet]
    public async Task<IActionResult> Get([FromQuery] TaskQueryParams query)
    {
        var userFarmIdClaim = User.FindFirst("farmId")?.Value;
        if (string.IsNullOrEmpty(userFarmIdClaim) && !User.IsInRole(RoleNames.Admin))
            return Forbid();

        var farmId = userFarmIdClaim is null ? (int?)null : int.Parse(userFarmIdClaim);

        var q = _db.FarmTasks
            .Include(t => t.TaskType)
            .Include(t => t.ExportPlan)
            .AsQueryable();

        if (!User.IsInRole(RoleNames.Admin))
        {
            q = q.Where(t => t.ExportPlan.FarmId == farmId);
        }

        if (query.TaskTypeId.HasValue)
            q = q.Where(t => t.TaskTypeId == query.TaskTypeId.Value);

        if (!string.IsNullOrEmpty(query.Status))
        {
            if (Enum.TryParse<Greenmind.Domain.Consts.Enums.TaskStatus>(query.Status, ignoreCase: true, out var status))
            {
                q = q.Where(t => t.Status == status);
            }
        }

        if (query.Priority.HasValue)
            q = q.Where(t => (int)t.Priority == query.Priority.Value);

        if (query.PlannedDateFrom.HasValue)
            q = q.Where(t => t.PlannedDate >= query.PlannedDateFrom.Value);

        if (query.PlannedDateTo.HasValue)
            q = q.Where(t => t.PlannedDate <= query.PlannedDateTo.Value);

        var skip = (Math.Max(1, query.Page) - 1) * Math.Max(1, query.PageSize);

        var items = await q
            .OrderBy(t => t.PlannedDate)
            .Skip(skip)
            .Take(Math.Max(1, query.PageSize))
            .Select(t => new TaskDto
            {
                Id = t.Id,
                ExportPlanId = t.ExportPlanId,
                TaskTypeId = t.TaskTypeId,
                TaskTypeName = t.TaskType.Name,
                Title = t.Title,
                Description = t.Description,
                PlannedDate = t.PlannedDate,
                ActualDate = t.ActualDate,
                Status = t.Status,
                Priority = t.Priority,
                Notes = t.Notes
            })
            .ToListAsync();

        return Ok(items);
    }

    [HttpPatch("{id:int}")]
    public async Task<IActionResult> Patch(int id, [FromBody] UpdateTaskRequest request)
    {
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        var userFarmIdClaim = User.FindFirst("farmId")?.Value;

        var task = await _db.FarmTasks.Include(t => t.ExportPlan).FirstOrDefaultAsync(t => t.Id == id);
        if (task == null) return NotFound();

        // Ownership check: platform admin can update any, farm owners only own their farm tasks
        if (!User.IsInRole(RoleNames.Admin))
        {
            if (string.IsNullOrEmpty(userFarmIdClaim)) return Forbid();
            var farmId = int.Parse(userFarmIdClaim);
            if (task.ExportPlan.FarmId != farmId) return Forbid();
        }

        // Apply allowed updates only
        if (request.Status is not null)
        {
            if (Enum.TryParse<Greenmind.Domain.Consts.Enums.TaskStatus>(request.Status, ignoreCase: true, out var status))
            {
                task.Status = status;
            }
            else
            {
                return BadRequest(new { Message = "Invalid status value." });
            }
        }

        if (request.ActualDate.HasValue)
            task.ActualDate = request.ActualDate;

        if (request.Notes is not null)
            task.Notes = request.Notes;

        // Validation: if completed require actual date
        if (task.Status == Greenmind.Domain.Consts.Enums.TaskStatus.Completed && !task.ActualDate.HasValue)
            return BadRequest(new { Message = "Completed tasks must have an actual date." });

        await _db.SaveChangesAsync();

        return NoContent();
    }
}
