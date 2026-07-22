using Greenmind.Application.Contracts.Persistence;
using Greenmind.Domain.Entities;

namespace Greenmind.Infrastructure.Persistence.Repositories;

public class SoilMeasurementRepository(GreenmindDbContext context)
    : GenericRepository<SoilMeasurement>(context), ISoilMeasurementRepository
{
}
