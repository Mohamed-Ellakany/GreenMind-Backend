using Greenmind.Application.Dtos.Admin;

namespace Greenmind.Application.Interfaces
{
    public interface IAdminFarmService
    {
        Task<IReadOnlyList<PendingFarmDto>> GetPendingRegistrationsAsync(CancellationToken cancellationToken = default);

        Task<FarmActionResultDto> ApproveFarmAsync(int farmId, CancellationToken cancellationToken = default);

        Task<FarmActionResultDto> ActivateFarmAsync(int farmId, CancellationToken cancellationToken = default);

        Task<FarmActionResultDto> DeactivateFarmAsync(int farmId, CancellationToken cancellationToken = default);
    }
}
