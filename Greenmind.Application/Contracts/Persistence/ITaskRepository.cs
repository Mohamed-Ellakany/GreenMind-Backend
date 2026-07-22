using DomainTask = Greenmind.Domain.Entities.FarmTask;

namespace Greenmind.Application.Contracts.Persistence;

public interface ITaskRepository : IGenericRepository<DomainTask>
{
}
