using Greenmind.Application.Contracts.Persistence;
using DomainTask = Greenmind.Domain.Entities.FarmTask;

namespace Greenmind.Infrastructure.Persistence.Repositories;

public class TaskRepository(GreenmindDbContext context)
    : GenericRepository<DomainTask>(context), ITaskRepository
{
}
