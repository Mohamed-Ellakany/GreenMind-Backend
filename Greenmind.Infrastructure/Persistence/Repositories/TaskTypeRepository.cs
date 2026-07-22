using Greenmind.Application.Contracts.Persistence;
using Greenmind.Domain.Entities;

namespace Greenmind.Infrastructure.Persistence.Repositories;

public class TaskTypeRepository(GreenmindDbContext context)
    : GenericRepository<TaskType>(context), ITaskTypeRepository
{
}
