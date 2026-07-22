using Greenmind.Application.Contracts.Persistence;
using Greenmind.Domain.Entities;

namespace Greenmind.Infrastructure.Persistence.Repositories;

public class DestinationRepository(GreenmindDbContext context)
    : GenericRepository<Destination>(context), IDestinationRepository
{
}
