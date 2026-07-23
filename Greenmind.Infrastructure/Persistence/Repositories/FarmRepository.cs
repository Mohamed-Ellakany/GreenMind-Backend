using Greenmind.Application.Interfaces;
using Greenmind.Application.Dtos.Admin;
using Greenmind.Domain.Consts.Enums;
using Greenmind.Domain.Entities;
using Greenmind.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Greenmind.Infrastructure.Persistence.Repositories
{
    public class FarmRepository(GreenmindDbContext dbContext) : IFarmRepository
    {
        public async Task<IReadOnlyList<PendingFarmDto>> GetPendingFarmsAsync(CancellationToken cancellationToken = default)
        {
            return await dbContext.Farms
                .AsNoTracking()
                .Where(f => f.Status == FarmStatus.Pending)
                .OrderBy(f => f.CreatedAt)
                .Select(f => new PendingFarmDto
                {
                    Id = f.Id,
                    Name = f.Name,
                    OwnerFullName = f.Owner.FullName,
                    OwnerEmail = f.Owner.Email!,
                    CreatedAt = f.CreatedAt
                })
                .ToListAsync(cancellationToken);
        }

        public Task<Farm?> GetFarmWithOwnerAsync(int farmId, CancellationToken cancellationToken = default)
        {
            return dbContext.Farms
                .Include(f => f.Owner)
                .FirstOrDefaultAsync(f => f.Id == farmId, cancellationToken);
        }

        public System.Threading.Tasks.Task SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            return dbContext.SaveChangesAsync(cancellationToken);
        }
    }
}
