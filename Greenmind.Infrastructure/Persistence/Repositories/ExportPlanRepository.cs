using Greenmind.Application.Contracts.Persistence;
using Greenmind.Domain.Entities;

namespace Greenmind.Infrastructure.Persistence.Repositories;

public class ExportPlanRepository(GreenmindDbContext context)
    : GenericRepository<ExportPlan>(context), IExportPlanRepository
{
}
