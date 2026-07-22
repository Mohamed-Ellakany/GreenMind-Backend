using Greenmind.Application.Contracts.Persistence;
using Greenmind.Domain.Entities;

namespace Greenmind.Infrastructure.Persistence.Repositories;

public class FarmRepository(GreenmindDbContext context)
    : GenericRepository<Farm>(context), IFarmRepository
{
}
