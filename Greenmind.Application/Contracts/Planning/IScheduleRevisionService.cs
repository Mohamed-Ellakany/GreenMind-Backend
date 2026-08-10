using Greenmind.Application.Dtos.Planning;

namespace Greenmind.Application.Contracts.Planning;

public interface IScheduleRevisionService
{
    Task<ScheduleRevisionDto?> QueueAsync(int exportPlanId, string source, CancellationToken cancellationToken = default);
    Task<ScheduleRevisionDto?> GetLatestAsync(int exportPlanId, CancellationToken cancellationToken = default);
    Task ProcessAsync(int revisionId, CancellationToken cancellationToken = default);
}
