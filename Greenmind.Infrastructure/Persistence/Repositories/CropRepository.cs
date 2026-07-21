using Greenmind.Application.Contracts.Persistence;
using Greenmind.Domain.Entities;

namespace Greenmind.Infrastructure.Persistence.Repositories;

public class CropRepository(GreenmindDbContext context)
    : GenericRepository<Crop>(context), ICropRepository
{
}
