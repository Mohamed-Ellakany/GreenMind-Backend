using Greenmind.Application.Dtos.Admin;
using Greenmind.Application.Exceptions;
using Greenmind.Application.Interfaces;
using Greenmind.Domain.Consts.Enums;

namespace Greenmind.Application.Services
{
    public class AdminFarmService(IFarmRepository farmRepository) : IAdminFarmService
    {
        public Task<IReadOnlyList<PendingFarmDto>> GetPendingRegistrationsAsync(CancellationToken cancellationToken = default)
        {
            return farmRepository.GetPendingFarmsAsync(cancellationToken);
        }

        public async Task<FarmActionResultDto> ApproveFarmAsync(int farmId, CancellationToken cancellationToken = default)
        {
            var farm = await GetFarmOrThrowAsync(farmId, cancellationToken);

            if (farm.Status != FarmStatus.Pending)
            {
                throw new BusinessRuleException("Farm has already been approved.", 409);
            }

            farm.Status = FarmStatus.Active;
            farm.Owner.IsActive = true;

            await farmRepository.SaveChangesAsync(cancellationToken);

            return MapToResult(farm);
        }

        public async Task<FarmActionResultDto> ActivateFarmAsync(int farmId, CancellationToken cancellationToken = default)
        {
            var farm = await GetFarmOrThrowAsync(farmId, cancellationToken);

            if (farm.Status == FarmStatus.Active)
            {
                throw new BusinessRuleException("Farm is already active.", 409);
            }

            if (farm.Status == FarmStatus.Pending)
            {
                throw new BusinessRuleException("Farm must be approved before it can be activated.", 400);
            }

            farm.Status = FarmStatus.Active;
            farm.Owner.IsActive = true;

            await farmRepository.SaveChangesAsync(cancellationToken);

            return MapToResult(farm);
        }

        public async Task<FarmActionResultDto> DeactivateFarmAsync(int farmId, CancellationToken cancellationToken = default)
        {
            var farm = await GetFarmOrThrowAsync(farmId, cancellationToken);

            if (farm.Status != FarmStatus.Active)
            {
                throw new BusinessRuleException("Only active farms can be deactivated.", 400);
            }

            farm.Status = FarmStatus.Inactive;
            farm.Owner.IsActive = false;

            await farmRepository.SaveChangesAsync(cancellationToken);

            return MapToResult(farm);
        }

        private async Task<Domain.Entities.Farm> GetFarmOrThrowAsync(int farmId, CancellationToken cancellationToken)
        {
            var farm = await farmRepository.GetFarmWithOwnerAsync(farmId, cancellationToken);

            if (farm is null)
            {
                throw new BusinessRuleException("Farm not found.", 404);
            }

            return farm;
        }

        private static FarmActionResultDto MapToResult(Domain.Entities.Farm farm)
        {
            return new FarmActionResultDto
            {
                FarmId = farm.Id,
                Name = farm.Name,
                Status = farm.Status.ToString(),
                OwnerIsActive = farm.Owner.IsActive
            };
        }
    }
}
