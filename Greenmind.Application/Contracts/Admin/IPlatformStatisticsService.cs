using Greenmind.Application.Dtos.Admin;

namespace Greenmind.Application.Contracts.Admin;

public interface IPlatformStatisticsService
{
    Task<PlatformStatisticsDto> GetAsync(CancellationToken cancellationToken = default);
}
