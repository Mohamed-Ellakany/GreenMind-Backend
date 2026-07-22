namespace Greenmind.Application.Contracts.Persistence;

public interface IUnitOfWork
{
    IFarmRepository Farms { get; }

    ICropRepository Crops { get; }

    IDestinationRepository Destinations { get; }

    IExportPlanRepository ExportPlans { get; }

    ITaskRepository Tasks { get; }

    ITaskTypeRepository TaskTypes { get; }

    ISoilMeasurementRepository SoilMeasurements { get; }

    System.Threading.Tasks.Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
