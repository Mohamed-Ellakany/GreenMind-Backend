using Greenmind.Domain.Consts.Enums;

namespace Greenmind.Application.Dtos.TaskProposals;

public sealed record ProposedTaskValuesDto(
    int TaskTypeId,
    string Title,
    string? Description,
    DateOnly PlannedDate,
    TaskPriority Priority);

public sealed record TaskOriginalValuesDto(
    int FarmTaskId,
    int TaskTypeId,
    string Title,
    string? Description,
    DateOnly PlannedDate,
    Greenmind.Domain.Consts.Enums.TaskStatus Status,
    TaskPriority Priority);

public sealed record TaskChangeProposalItemDto(
    int Id,
    TaskChangeOperation Operation,
    int? FarmTaskId,
    TaskOriginalValuesDto? OriginalValues,
    ProposedTaskValuesDto ProposedValues,
    string Reason);

public sealed record TaskChangeProposalDto(
    int Id,
    int ExportPlanId,
    TaskChangeProposalStatus Status,
    DateTime ExpiresAtUtc,
    DateTime? ReviewedAtUtc,
    int? SourceChatMessageId,
    int? SourceDiseaseReportId,
    IReadOnlyList<TaskChangeProposalItemDto> Items);
