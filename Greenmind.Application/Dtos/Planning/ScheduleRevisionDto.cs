namespace Greenmind.Application.Dtos.Planning;

public sealed record ScheduleRevisionDto(
    int Id,
    int ExportPlanId,
    string Status,
    string Source,
    DateTime CreatedAt,
    DateTime? StartedAtUtc,
    DateTime? CompletedAtUtc,
    int AttemptCount,
    string? FailureMessage,
    IReadOnlyList<ScheduleRevisionItemDto> Items);

public sealed record ScheduleRevisionItemDto(
    int Id,
    int? FarmTaskId,
    string Operation,
    string TaskTitle,
    DateOnly? PreviousPlannedDate,
    DateOnly? NewPlannedDate,
    string? Reason);
