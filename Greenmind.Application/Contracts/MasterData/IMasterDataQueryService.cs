using Greenmind.Application.Dtos.MasterData;

namespace Greenmind.Application.Contracts.MasterData;

public interface IMasterDataQueryService
{
    Task<IReadOnlyList<MasterDataItemDto>> GetActiveCropsAsync(
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<MasterDataItemDto>> GetActiveDestinationsAsync(
        CancellationToken cancellationToken = default);
}
