using Greenmind.Application.Contracts.Persistence;

namespace Greenmind.Infrastructure.Persistence.Repositories;

public class UnitOfWork : IUnitOfWork
{
    private readonly GreenmindDbContext _context;

    public UnitOfWork(GreenmindDbContext context)
    {
        _context = context;
        Farms = new FarmRepository(_context);
        Crops = new CropRepository(_context);
        Destinations = new DestinationRepository(_context);
        ExportPlans = new ExportPlanRepository(_context);
        Tasks = new TaskRepository(_context);
        TaskTypes = new TaskTypeRepository(_context);
        SoilMeasurements = new SoilMeasurementRepository(_context);
    }

    public IFarmRepository Farms { get; }

    public ICropRepository Crops { get; }

    public IDestinationRepository Destinations { get; }

    public IExportPlanRepository ExportPlans { get; }

    public ITaskRepository Tasks { get; }

    public ITaskTypeRepository TaskTypes { get; }

    public ISoilMeasurementRepository SoilMeasurements { get; }

    public async System.Threading.Tasks.Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        return await _context.SaveChangesAsync(cancellationToken);
    }
}
