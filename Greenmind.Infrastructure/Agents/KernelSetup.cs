using Greenmind.Application.Contracts.Persistence;
using Greenmind.Application.Interfaces.AI;
using Greenmind.Infrastructure.Agents.Plugins;
using Greenmind.Infrastructure.Agents.Prompts;
using Greenmind.Infrastructure.Iti;
using Greenmind.Infrastructure.Persistence;
using Greenmind.Infrastructure.Persistence.Repositories;
using Greenmind.Infrastructure.Rag;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Greenmind.Infrastructure.Agents;

public static class KernelSetup
{
    public static void AddFarmPlanningKernel(this IServiceCollection services, IConfiguration config)
    {
        services.AddHttpClient<IWeatherHttpClient, WeatherHttpClient>(c =>
        {
            c.BaseAddress = CreateWeatherBaseAddress(config["Weather:BaseUrl"]);
            c.DefaultRequestHeaders.Add("x-api-key", config["Weather:ApiKey"]);
        });

        // Soil is DB-backed now, not HTTP
        // NOTE: fully-qualified here because both ISoilMeasurementRepository AND SoilMeasurementRepository
        // exist twice in this project (Application.Interfaces.AI vs Application.Contracts.Persistence /
        // Infrastructure.Persistence vs Infrastructure.Persistence.Repositories) - these RAG registrations
        // below need "using"s for the Contracts.Persistence / Persistence.Repositories namespaces too,
        // which makes the short names ambiguous. This is the AI-plugin variant (GetLatestAsync/GetHistoryAsync),
        // not the generic-repository variant used by IUnitOfWork.
        services.AddScoped<Greenmind.Application.Interfaces.AI.ISoilMeasurementRepository,
                            Greenmind.Infrastructure.Persistence.SoilMeasurementRepository>();
        services.AddScoped<ITaskHistoryRepository, TaskHistoryRepository>();
        services.AddScoped<IFarmTaskPersistenceService, FarmTaskPersistenceService>();
        services.AddScoped<FarmPlanningPromptBuilder>();

        services.AddHttpClient<IItiBedrockGatewayClient, ItiBedrockGatewayClient>(client =>
        {
            client.BaseAddress = CreateItiBaseAddress(config["iti:BaseUrl"]);
            client.Timeout = TimeSpan.FromSeconds(120);
        });

        services.AddScoped<IComplianceDocumentRepository, ComplianceDocumentRepository>();
        services.AddScoped<IComplianceIngestionService, ComplianceIngestionService>();
        services.AddScoped<ComplianceRagPlugin>();
        // ---------------------------------------------------------------------------------

        services.AddScoped<WeatherPlugin>();
        services.AddScoped<SoilPlugin>();
        services.AddScoped<TaskHistoryPlugin>();
        services.AddScoped<RegulatoryDataPlugin>();

        services.AddScoped<IAgentPlanningService, AgentPlanningService>();
    }

    public static Uri CreateWeatherBaseAddress(string? baseUrl)
    {
        if (string.IsNullOrWhiteSpace(baseUrl))
        {
            throw new InvalidOperationException("Weather:BaseUrl must be configured.");
        }

        var normalized = baseUrl.EndsWith("/", StringComparison.Ordinal)
            ? baseUrl
            : $"{baseUrl}/";

        return new Uri(normalized, UriKind.Absolute);
    }

    public static Uri CreateItiBaseAddress(string? baseUrl)
    {
        var value = string.IsNullOrWhiteSpace(baseUrl)
            ? "http://apiaccess.iti.net.eg/api/v1/"
            : baseUrl;
        var normalized = value.EndsWith("/", StringComparison.Ordinal)
            ? value
            : $"{value}/";

        return new Uri(normalized, UriKind.Absolute);
    }
}
