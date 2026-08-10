using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Greenmind.Infrastructure.Persistence;
using Greenmind.Api.Models;
using Greenmind.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using Greenmind.Domain.Consts.Enums;
using Greenmind.Domain.Consts;
using Greenmind.Application.Contracts.Planning;
using DomainTaskStatus = Greenmind.Domain.Consts.Enums.TaskStatus;

namespace Greenmind.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = RoleNames.FarmOwner + "," + RoleNames.Admin)]
public class TasksController : ControllerBase
{
    private readonly GreenmindDbContext _db;

    public TasksController(GreenmindDbContext db) => _db = db;

    [HttpGet]
    public async Task<IActionResult> Get([FromQuery] TaskQueryParams query, CancellationToken cancellationToken)
    {
        var farmId = await GetFarmIdAsync(cancellationToken);
        if (!User.IsInRole(RoleNames.Admin) && !farmId.HasValue) return Forbid();

        var tasks = _db.FarmTasks
            .AsNoTracking()
            .Include(task => task.TaskType)
            .Include(task => task.ExportPlan)
            .AsQueryable();

        if (!User.IsInRole(RoleNames.Admin))
            tasks = tasks.Where(task => task.ExportPlan.FarmId == farmId);
        if (query.ExportPlanId.HasValue)
            tasks = tasks.Where(task => task.ExportPlanId == query.ExportPlanId.Value);
        if (query.TaskTypeId.HasValue)
            tasks = tasks.Where(task => task.TaskTypeId == query.TaskTypeId.Value);
        if (!string.IsNullOrWhiteSpace(query.Status) && Enum.TryParse<DomainTaskStatus>(query.Status, true, out var status))
            tasks = tasks.Where(task => task.Status == status);
        if (!string.IsNullOrWhiteSpace(query.Origin) && Enum.TryParse<TaskOrigin>(query.Origin, true, out var origin))
            tasks = tasks.Where(task => task.Origin == origin);
        if (query.Priority.HasValue)
            tasks = tasks.Where(task => (int)task.Priority == query.Priority.Value);
        if (query.PlannedDateFrom.HasValue)
            tasks = tasks.Where(task => task.PlannedDate >= query.PlannedDateFrom.Value);
        if (query.PlannedDateTo.HasValue)
            tasks = tasks.Where(task => task.PlannedDate <= query.PlannedDateTo.Value);
        if (!string.IsNullOrWhiteSpace(query.Search))
            tasks = tasks.Where(task => task.Title.Contains(query.Search) || (task.Description != null && task.Description.Contains(query.Search)));

        var skip = (Math.Max(1, query.Page) - 1) * Math.Max(1, query.PageSize);
        var loadedTasks = await tasks
            .OrderBy(task => task.PlannedDate)
            .ThenByDescending(task => task.Priority)
            .Skip(skip)
            .Take(Math.Min(200, Math.Max(1, query.PageSize)))
            .ToListAsync(cancellationToken);

        return Ok(loadedTasks.Select(task => MapTask(task, null)).ToList());
    }

    [HttpGet("types")]
    public async Task<ActionResult<IReadOnlyList<TaskTypeDto>>> Types(CancellationToken cancellationToken)
    {
        var farmId = await GetFarmIdAsync(cancellationToken);
        if (!User.IsInRole(RoleNames.Admin) && !farmId.HasValue) return Forbid();

        return Ok(await _db.TaskTypes.AsNoTracking().OrderBy(type => type.Id)
            .Select(type => new TaskTypeDto { Id = type.Id, Name = type.Name, Description = type.Description })
            .ToListAsync(cancellationToken));
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<TaskDto>> GetById(int id, CancellationToken cancellationToken)
    {
        var task = await LoadOwnedTaskAsync(id, cancellationToken);
        return task is null ? NotFound() : Ok(MapTask(task, task.Activities.OrderByDescending(item => item.CreatedAt)));
    }

    [HttpPost]
    public async Task<ActionResult<TaskDto>> Create([FromBody] CreateTaskRequest request, CancellationToken cancellationToken)
    {
        if (!Enum.TryParse<DomainTaskStatus>(request.Status, true, out var status))
            return BadRequest(new { Message = "Invalid status value." });
        if (request.PlannedDate == DateOnly.MinValue)
            return BadRequest(new { Message = "A planned date is required." });
        if (status == DomainTaskStatus.Completed && !request.ActualDate.HasValue)
            return BadRequest(new { Message = "Completed tasks must have an actual date." });
        if (status == DomainTaskStatus.Postponed)
            return BadRequest(new { Message = "A postponed task must start from an existing task update." });

        var farmId = await GetFarmIdAsync(cancellationToken);
        var plan = await _db.ExportPlans.Include(item => item.Farm)
            .SingleOrDefaultAsync(item => item.Id == request.ExportPlanId, cancellationToken);
        if (plan is null) return NotFound(new { Message = "Export plan not found." });
        if (!User.IsInRole(RoleNames.Admin) && plan.FarmId != farmId) return Forbid();
        if (!await _db.TaskTypes.AnyAsync(type => type.Id == request.TaskTypeId, cancellationToken))
            return BadRequest(new { Message = "Task type not found." });

        var task = new FarmTask
        {
            ExportPlanId = request.ExportPlanId,
            TaskTypeId = request.TaskTypeId,
            Title = request.Title.Trim(),
            Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim(),
            Quantity = request.Quantity,
            Unit = request.Unit.Trim(),
            PlannedDate = request.PlannedDate,
            ActualDate = request.ActualDate,
            Status = status,
            Priority = request.Priority,
            Origin = TaskOrigin.UserAdded,
            Notes = string.IsNullOrWhiteSpace(request.Notes) ? null : request.Notes.Trim()
        };
        task.Activities.Add(new FarmTaskActivity
        {
            EventType = "Created",
            Source = "User",
            ActorUserId = User.FindFirstValue(ClaimTypes.NameIdentifier),
            ActorName = User.Identity?.Name,
            NewStatus = task.Status,
            NewPlannedDate = task.PlannedDate,
            ActualDate = task.ActualDate,
            Note = task.Notes
        });

        _db.FarmTasks.Add(task);
        await _db.SaveChangesAsync(cancellationToken);
        await RevisionService().QueueAsync(plan.Id, "TaskCreated", cancellationToken);
        await _db.Entry(task).Reference(item => item.TaskType).LoadAsync(cancellationToken);
        await _db.Entry(task).Collection(item => item.Activities).LoadAsync(cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id = task.Id }, MapTask(task, task.Activities));
    }

    [HttpPatch("{id:int}")]
    public async Task<IActionResult> Patch(int id, [FromBody] UpdateTaskRequest request, CancellationToken cancellationToken)
    {
        var task = await LoadTaskAsync(id, cancellationToken);
        if (task is null) return NotFound();
        if (!User.IsInRole(RoleNames.Admin))
        {
            var farmId = await GetFarmIdAsync(cancellationToken);
            if (!farmId.HasValue || farmId.Value != task.ExportPlan.FarmId) return Forbid();
        }

        var previousStatus = task.Status;
        var previousDate = task.PlannedDate;
        var requestedStatus = task.Status;
        if (request.Status is not null && !Enum.TryParse<DomainTaskStatus>(request.Status, true, out requestedStatus))
            return BadRequest(new { Message = "Invalid status value." });

        if (requestedStatus == DomainTaskStatus.Postponed && (!request.PlannedDate.HasValue || request.PlannedDate.Value <= task.PlannedDate))
            return BadRequest(new { Message = "Postponed tasks require a later planned date." });
        if (requestedStatus == DomainTaskStatus.Completed && !(request.ActualDate ?? task.ActualDate).HasValue)
            return BadRequest(new { Message = "Completed tasks must have an actual date." });

        task.Status = requestedStatus;
        if (request.PlannedDate.HasValue) task.PlannedDate = request.PlannedDate.Value;
        if (request.ActualDate.HasValue) task.ActualDate = request.ActualDate;
        if (request.Notes is not null) task.Notes = request.Notes.Trim();

        var changed = previousStatus != task.Status || previousDate != task.PlannedDate || request.ActualDate.HasValue || request.Notes is not null;
        if (changed)
        {
            task.Activities.Add(new FarmTaskActivity
            {
                EventType = previousStatus != task.Status ? "StatusChanged" : previousDate != task.PlannedDate ? "Rescheduled" : "Updated",
                Source = "User",
                ActorUserId = User.FindFirstValue(ClaimTypes.NameIdentifier),
                ActorName = User.Identity?.Name,
                PreviousStatus = previousStatus,
                NewStatus = task.Status,
                PreviousPlannedDate = previousDate,
                NewPlannedDate = task.PlannedDate,
                ActualDate = task.ActualDate,
                Note = request.Notes
            });
        }

        await _db.SaveChangesAsync(cancellationToken);
        if (changed)
            await RevisionService().QueueAsync(task.ExportPlanId, "TaskUpdated", cancellationToken);
        return NoContent();
    }

    private async Task<FarmTask?> LoadOwnedTaskAsync(int id, CancellationToken cancellationToken)
    {
        var task = await LoadTaskAsync(id, cancellationToken);
        if (task is null) return null;
        var farmId = await GetFarmIdAsync(cancellationToken);
        return User.IsInRole(RoleNames.Admin) || farmId == task.ExportPlan.FarmId ? task : null;
    }

    private Task<FarmTask?> LoadTaskAsync(int id, CancellationToken cancellationToken) =>
        _db.FarmTasks
            .Include(item => item.TaskType)
            .Include(item => item.ExportPlan)
            .Include(item => item.Activities)
            .SingleOrDefaultAsync(item => item.Id == id, cancellationToken);

    private async Task<int?> GetFarmIdAsync(CancellationToken cancellationToken)
    {
        if (int.TryParse(User.FindFirst("farmId")?.Value, out var claimFarmId)) return claimFarmId;
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return string.IsNullOrWhiteSpace(userId)
            ? null
            : await _db.Farms.Where(farm => farm.OwnerUserId == userId).Select(farm => (int?)farm.Id).SingleOrDefaultAsync(cancellationToken);
    }

    private IScheduleRevisionService RevisionService() =>
        HttpContext.RequestServices.GetRequiredService<IScheduleRevisionService>();

    private static TaskDto MapTask(FarmTask task, IEnumerable<FarmTaskActivity>? activities)
    {
        return new TaskDto
        {
            Id = task.Id,
            ExportPlanId = task.ExportPlanId,
            TaskTypeId = task.TaskTypeId,
            TaskTypeName = task.TaskType?.Name ?? string.Empty,
            Title = task.Title,
            Description = task.Description,
            PlannedDate = task.PlannedDate,
            ActualDate = task.ActualDate,
            Status = task.Status,
            Priority = task.Priority,
            Origin = task.Origin,
            Notes = task.Notes,
            Activities = activities?.Select(activity => new TaskActivityDto
            {
                Id = activity.Id,
                EventType = activity.EventType,
                Source = activity.Source,
                ActorName = activity.ActorName,
                PreviousStatus = activity.PreviousStatus,
                NewStatus = activity.NewStatus,
                PreviousPlannedDate = activity.PreviousPlannedDate,
                NewPlannedDate = activity.NewPlannedDate,
                ActualDate = activity.ActualDate,
                Note = activity.Note,
                CreatedAt = activity.CreatedAt
            }).ToList()
        };
    }
}
