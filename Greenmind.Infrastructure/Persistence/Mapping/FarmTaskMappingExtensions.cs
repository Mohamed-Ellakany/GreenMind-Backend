using Greenmind.Application.Dtos.AI;
using Greenmind.Domain.Entities;
using DomainTaskStatus = Greenmind.Domain.Consts.Enums.TaskStatus;
using Greenmind.Domain.Consts.Enums;

namespace Greenmind.Infrastructure.Persistence.Mapping
{
    public static class FarmTaskMappingExtensions
    {
        public static TaskItemDto ToDto(this FarmTask entity) => new()
        {
            TaskTypeName = entity.TaskType?.Name ?? string.Empty, 
            Title = entity.Title,
            Description = entity.Description ?? string.Empty,
            Quantity = entity.Quantity,
            Unit = entity.Unit,
            PlannedDate = entity.PlannedDate,
            ActualDate = entity.ActualDate,
            Status = entity.Status.ToString(),
            Priority = entity.Priority,
            Notes = entity.Notes,
            Origin = entity.Origin.ToString(),
            Activities = entity.Activities.OrderByDescending(activity => activity.CreatedAt).Select(activity => new TaskActivityItemDto
            {
                EventType = activity.EventType,
                Source = activity.Source,
                Note = activity.Note,
                PreviousStatus = activity.PreviousStatus?.ToString(),
                NewStatus = activity.NewStatus?.ToString(),
                PreviousPlannedDate = activity.PreviousPlannedDate,
                NewPlannedDate = activity.NewPlannedDate,
                CreatedAt = activity.CreatedAt
            }).ToList()
        };

        public static TaskPriority ToPriorityEnum(this string priority) =>
            Enum.TryParse<TaskPriority>(priority, ignoreCase: true, out var result)
                ? result
                : TaskPriority.Medium;

        public static TaskPriority ToPriorityEnum(this int priority) =>
      Enum.IsDefined(typeof(TaskPriority), priority)
          ? (TaskPriority)priority
          : TaskPriority.Medium;

        public static DomainTaskStatus ToTaskStatusEnum(this string status) =>
            Enum.TryParse<DomainTaskStatus>(status, ignoreCase: true, out var result)
                ? result
                : DomainTaskStatus.Pending;
    }
}
