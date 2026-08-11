using Greenmind.Application.Dtos.TaskProposals;

namespace Greenmind.Application.Interfaces.AI;

public interface IPlanTaskProposalGenerator
{
    Task<IReadOnlyList<GeneratedTaskChangeItem>> TryGenerateAsync(
        string userRequest,
        string planContextJson,
        CancellationToken cancellationToken = default,
        int? exportPlanId = null);
}

public sealed record GeneratedTaskChangeItem(
    string Operation,
    int? FarmTaskId,
    ProposedTaskValuesDto ProposedValues,
    string Reason);
