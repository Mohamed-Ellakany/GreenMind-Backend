using Greenmind.Domain.Consts.Enums;
using System.Text.Json.Serialization;

namespace Greenmind.Application.Dtos.TaskProposals;

[method: JsonConstructor]
public sealed record ProposedTaskValuesDto(
    int TaskTypeId,
    string Title,
    string? Description,
    decimal Quantity,
    string Unit,
    DateOnly PlannedDate,
    TaskPriority Priority)
{
    public ProposedTaskValuesDto(
        int taskTypeId,
        string title,
        string? description,
        DateOnly plannedDate,
        TaskPriority priority)
        : this(taskTypeId, title, description, 1, "وحدة", plannedDate, priority)
    {
    }
}

[method: JsonConstructor]
public sealed record TaskOriginalValuesDto(
    int FarmTaskId,
    int TaskTypeId,
    string Title,
    string? Description,
    decimal Quantity,
    string Unit,
    DateOnly PlannedDate,
    Greenmind.Domain.Consts.Enums.TaskStatus Status,
    TaskPriority Priority)
{
    public TaskOriginalValuesDto(
        int farmTaskId,
        int taskTypeId,
        string title,
        string? description,
        DateOnly plannedDate,
        Greenmind.Domain.Consts.Enums.TaskStatus status,
        TaskPriority priority)
        : this(farmTaskId, taskTypeId, title, description, 1, "وحدة", plannedDate, status, priority)
    {
    }
}

public sealed record TaskChangeProposalItemDto(
    int Id,
    TaskChangeOperation Operation,
    int? FarmTaskId,
    TaskOriginalValuesDto? OriginalValues,
    ProposedTaskValuesDto ProposedValues,
    string Reason,
    bool IsIncluded);

public sealed record TaskChangeProposalDto(
    int Id,
    int ExportPlanId,
    TaskChangeProposalStatus Status,
    DateTime ExpiresAtUtc,
    DateTime? ReviewedAtUtc,
    int? SourceChatMessageId,
    int? SourceDiseaseReportId,
    IReadOnlyList<TaskChangeProposalItemDto> Items);

public sealed record UpdateTaskProposalItemDto(
    int Id,
    bool IsIncluded,
    string Title,
    string? Description,
    decimal Quantity,
    string Unit,
    DateOnly PlannedDate,
    TaskPriority Priority);

public sealed record UpdateTaskProposalRequest(
    IReadOnlyList<UpdateTaskProposalItemDto> Items);
