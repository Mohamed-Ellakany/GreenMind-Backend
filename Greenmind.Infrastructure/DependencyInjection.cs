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
using Greenmind.Application.Contracts.Regulatory;
using Greenmind.Infrastructure.Regulatory;
using Greenmind.Application.Contracts.Chat;
using Greenmind.Application.Interfaces.AI;
using Greenmind.Infrastructure.Agents;
using Greenmind.Infrastructure.Chat;
using Greenmind.Application.Contracts.TaskProposals;
using Greenmind.Infrastructure.TaskProposals;
using Greenmind.Application.Contracts.Costs;
using Greenmind.Infrastructure.Costs;
using Greenmind.Application.Contracts.Storage;
using Greenmind.Infrastructure.Storage;
using Greenmind.Application.Contracts.Disease;
using Greenmind.Infrastructure.Disease;
using Greenmind.Application.Contracts.Support;
using Greenmind.Infrastructure.Support;

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
        services.AddScoped<Greenmind.Application.Contracts.Persistence.ISoilMeasurementRepository, SoilMeasurementRepository>();
        services.AddScoped<Greenmind.Application.Services.IFuturePlannedDaysCalculationService, FuturePlannedDaysCalculationService>();
        services.AddScoped<IUnitOfWork, UnitOfWork>();
        services.AddScoped<IPlatformAdminService, PlatformAdminService>();
        services.AddScoped<IAiUsageAdminService, AiUsageAdminService>();
        services.AddScoped<IAiLimitRequestService, AiLimitRequestService>();
        services.AddScoped<IPlatformStatisticsService, PlatformStatisticsService>();
        services.AddScoped<IMasterDataQueryService, PlatformAdminService>();
        services.AddSingleton<TimeProvider>(TimeProvider.System);
        services.AddSingleton<ISeasonCalculationService, SeasonCalculationService>();
        services.AddScoped<IExportPlanDashboardService, ExportPlanDashboardService>();
        services.AddScoped<IExportPlanService, ExportPlanService>();
        services.AddScoped<IExportScheduleAdvisor, TrustedExportScheduleAdvisor>();
        services.AddMemoryCache();
        services.AddHttpClient<IClimateDataClient, NasaPowerClimateClient>(client =>
        {
            client.BaseAddress = new Uri("https://power.larc.nasa.gov/");
            client.Timeout = TimeSpan.FromSeconds(30);
        });
        services.AddScoped<ISoilMeasurementService, SoilMeasurementService>();
        services.AddScoped<IExportPlanPlanningService, ExportPlanPlanningService>();
        services.AddScoped<IScheduleRevisionService, ScheduleRevisionService>();
        services.AddScoped<Greenmind.Application.Services.IPasswordHashingService, PasswordHashingService>();
        services.AddScoped<Greenmind.Application.Services.IJwtTokenService, JwtTokenService>();
        services.AddScoped<IRegulatoryDataImportService, RegulatoryDataImportService>();
        services.AddScoped<IRegulatoryKnowledgeQueryService, RegulatoryKnowledgeQueryService>();
        services.AddScoped<IConversationAiService, ConversationAiService>();
        services.AddScoped<IChatService, ChatService>();
        services.AddScoped<ITaskChangeProposalService, TaskChangeProposalService>();
        services.AddSingleton<IPlanTaskProposalGenerator, PlanTaskProposalGenerator>();
        services.AddScoped<ICostManagementService, CostManagementService>();
        services.AddScoped<IExpenseAttachmentService, ExpenseAttachmentService>();
        services.AddSingleton<IPrivateFileStorage, ServerPrivateFileStorage>();
        services.AddScoped<IDiseaseReportService, DiseaseReportService>();
        services.AddScoped<IDiagnosisSessionService, DiagnosisSessionService>();
        services.AddScoped<IDiagnosisConversationAiService, DiagnosisConversationAiService>();
        services.AddScoped<ISupportService, SupportService>();
        services.AddHttpClient<IDiagnosisKnowledgeProvider, TrustedDiagnosisKnowledgeProvider>(client =>
        {
            client.Timeout = TimeSpan.FromSeconds(15);
            client.DefaultRequestHeaders.UserAgent.ParseAdd("GreenMind-Diagnosis/1.0");
        });
        services.AddSingleton<IDiseaseVisionAnalyzer, DiseaseVisionAnalyzer>();

        return services;
    }
}
