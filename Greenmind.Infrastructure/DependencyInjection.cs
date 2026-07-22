using Greenmind.Application.Contracts.Persistence;
using Greenmind.Application.Contracts.Admin;
using Greenmind.Infrastructure.Admin;
using Greenmind.Infrastructure.Persistence.Repositories;
using Microsoft.Extensions.DependencyInjection;

namespace Greenmind.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructureServices(this IServiceCollection services)
    {
        services.AddScoped(typeof(IGenericRepository<>), typeof(GenericRepository<>));
        services.AddScoped<IFarmRepository, FarmRepository>();
        services.AddScoped<ICropRepository, CropRepository>();
        services.AddScoped<IDestinationRepository, DestinationRepository>();
        services.AddScoped<IExportPlanRepository, ExportPlanRepository>();
        services.AddScoped<ITaskRepository, TaskRepository>();
        services.AddScoped<ITaskTypeRepository, TaskTypeRepository>();
        services.AddScoped<ISoilMeasurementRepository, SoilMeasurementRepository>();
        services.AddScoped<IUnitOfWork, UnitOfWork>();
        services.AddScoped<IPlatformAdminService, PlatformAdminService>();

        return services;
    }
}
