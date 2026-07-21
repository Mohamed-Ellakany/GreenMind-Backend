using DomainTask = Greenmind.Domain.Entities.Task;

namespace Greenmind.Application.Contracts.Persistence;

public interface ITaskRepository : IGenericRepository<DomainTask>
{
}
