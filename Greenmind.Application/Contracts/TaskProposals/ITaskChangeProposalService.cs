using Greenmind.Application.Dtos.TaskProposals;
using Greenmind.Application.Interfaces.AI;

namespace Greenmind.Application.Contracts.TaskProposals;

public interface ITaskChangeProposalService
{
    Task<TaskProposalResult<TaskChangeProposalDto?>> CreateFromAiAsync(
        int exportPlanId,
        int sourceChatMessageId,
        IReadOnlyList<GeneratedTaskChangeItem> generatedItems,
        CancellationToken cancellationToken = default);

    Task<TaskProposalResult<TaskChangeProposalDto>> GetAsync(
        int proposalId,
        string ownerUserId,
        CancellationToken cancellationToken = default);

    Task<TaskProposalResult<TaskChangeProposalDto>> ApplyAsync(
        int proposalId,
        string ownerUserId,
        CancellationToken cancellationToken = default);

    Task<TaskProposalResult<TaskChangeProposalDto>> RejectAsync(
        int proposalId,
        string ownerUserId,
        CancellationToken cancellationToken = default);
}

public enum TaskProposalResultStatus
{
    Succeeded,
    NotFound,
    Invalid
}

public sealed record TaskProposalResult<T>(
    TaskProposalResultStatus Status,
    T? Value = default,
    string? ErrorCode = null,
    string? ErrorMessage = null)
{
    public static TaskProposalResult<T> Success(T value) =>
        new(TaskProposalResultStatus.Succeeded, value);

    public static TaskProposalResult<T> NotFound() =>
        new(TaskProposalResultStatus.NotFound);

    public static TaskProposalResult<T> Invalid(string code, string message) =>
        new(TaskProposalResultStatus.Invalid, default, code, message);
}
