using Greenmind.Application.Dtos.Admin;
using Greenmind.Application.Dtos.MasterData;

namespace Greenmind.Application.Contracts.Admin;

public interface IPlatformAdminService
{
    Task<PagedResult<FarmAccountDto>> GetFarmsAsync(
        FarmAccountStatus? status,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken = default);

    Task<AdminOperationResult<FarmAccountDto>> ApproveFarmAsync(
        int farmId,
        CancellationToken cancellationToken = default);

    Task<AdminOperationResult<FarmAccountDto>> SetFarmAccountStatusAsync(
        int farmId,
        bool isActive,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<MasterDataItemDto>> GetCropsAsync(bool includeDeleted = false, CancellationToken cancellationToken = default);
    Task<AdminOperationResult<MasterDataItemDto>> CreateCropAsync(MasterDataRequest request, CancellationToken cancellationToken = default);
    Task<AdminOperationResult<MasterDataItemDto>> UpdateCropAsync(int id, MasterDataRequest request, CancellationToken cancellationToken = default);
    Task<AdminOperationResult<MasterDataItemDto>> SetCropStatusAsync(int id, bool isActive, CancellationToken cancellationToken = default);
    Task<AdminOperationResult<MasterDataItemDto>> DeleteCropAsync(int id, CancellationToken cancellationToken = default);
    Task<AdminOperationResult<MasterDataItemDto>> RestoreCropAsync(int id, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<MasterDataItemDto>> GetDestinationsAsync(bool includeDeleted = false, CancellationToken cancellationToken = default);
    Task<AdminOperationResult<MasterDataItemDto>> CreateDestinationAsync(MasterDataRequest request, CancellationToken cancellationToken = default);
    Task<AdminOperationResult<MasterDataItemDto>> UpdateDestinationAsync(int id, MasterDataRequest request, CancellationToken cancellationToken = default);
    Task<AdminOperationResult<MasterDataItemDto>> SetDestinationStatusAsync(int id, bool isActive, CancellationToken cancellationToken = default);
    Task<AdminOperationResult<MasterDataItemDto>> DeleteDestinationAsync(int id, CancellationToken cancellationToken = default);
    Task<AdminOperationResult<MasterDataItemDto>> RestoreDestinationAsync(int id, CancellationToken cancellationToken = default);
}
