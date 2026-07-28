using Greenmind.Application.Contracts.Persistence;
using Greenmind.Application.Contracts.Admin;
using Greenmind.Application.Contracts.MasterData;
using Greenmind.Application.Contracts.Dashboard;
using Greenmind.Application.Contracts.ExportPlans;
using Greenmind.Application.Contracts.Soil;
using Greenmind.Application.Services;
using Greenmind.Infrastructure.Admin;
using Greenmind.Infrastructure.Dashboard;
using Greenmind.Infrastructure.ExportPlans;
using Greenmind.Infrastructure.Persistence.Repositories;
using Greenmind.Infrastructure.Soil;
using Microsoft.Extensions.DependencyInjection;
using Greenmind.Infrastructure.Services;
using Greenmind.Application.Contracts.Planning;
using Greenmind.Infrastructure.Planning;

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
        services.AddScoped<Greenmind.Application.Services.IFuturePlannedDaysCalculationService, FuturePlannedDaysCalculationService>();
        services.AddScoped<IUnitOfWork, UnitOfWork>();
        services.AddScoped<IPlatformAdminService, PlatformAdminService>();
        services.AddScoped<IPlatformStatisticsService, PlatformStatisticsService>();
        services.AddScoped<IMasterDataQueryService, PlatformAdminService>();
        services.AddSingleton<TimeProvider>(TimeProvider.System);
        services.AddSingleton<ISeasonCalculationService, SeasonCalculationService>();
        services.AddScoped<IExportPlanDashboardService, ExportPlanDashboardService>();
        services.AddScoped<IExportPlanService, ExportPlanService>();
        services.AddScoped<ISoilMeasurementService, SoilMeasurementService>();
        services.AddScoped<IExportPlanPlanningService, ExportPlanPlanningService>();
        services.AddScoped<Greenmind.Application.Services.IPasswordHashingService, PasswordHashingService>();
        services.AddScoped<Greenmind.Application.Services.IJwtTokenService, JwtTokenService>();

        return services;
    }
}
