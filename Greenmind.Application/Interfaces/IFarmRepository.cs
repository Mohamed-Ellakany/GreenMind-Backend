using Greenmind.Application.Dtos.Admin;

namespace Greenmind.Application.Interfaces
{
    public interface IFarmRepository
    {
        Task<IReadOnlyList<PendingFarmDto>> GetPendingFarmsAsync(CancellationToken cancellationToken = default);

        Task<Domain.Entities.Farm?> GetFarmWithOwnerAsync(int farmId, CancellationToken cancellationToken = default);

        Task SaveChangesAsync(CancellationToken cancellationToken = default);
    }
}
