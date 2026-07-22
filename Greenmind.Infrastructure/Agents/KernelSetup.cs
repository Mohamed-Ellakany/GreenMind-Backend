using Greenmind.Application.Interfaces.AI;
using Greenmind.Infrastructure.Agents.Plugins;
using Greenmind.Infrastructure.Agents.Prompts;
using Greenmind.Infrastructure.Persistence;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.SemanticKernel;

namespace Greenmind.Infrastructure.Agents;
public static class KernelSetup
{
    public static void AddFarmPlanningKernel(this IServiceCollection services, IConfiguration config)
    {
        services.AddHttpClient<IWeatherHttpClient, WeatherHttpClient>(c =>
        {
            c.BaseAddress = new Uri(config["Weather:BaseUrl"]!);
            c.DefaultRequestHeaders.Add("x-api-key", config["Weather:ApiKey"]);
        });

        // Soil is DB-backed now, not HTTP
        services.AddScoped<ISoilMeasurementRepository, SoilMeasurementRepository>();
        services.AddScoped<ITaskHistoryRepository, TaskHistoryRepository>();
        services.AddScoped<IFarmTaskPersistenceService, FarmTaskPersistenceService>();
        services.AddScoped<FarmPlanningPromptBuilder>();

        services.AddScoped<WeatherPlugin>();
        services.AddScoped<SoilPlugin>();
        services.AddScoped<TaskHistoryPlugin>();

        services.AddScoped<IAgentPlanningService, AgentPlanningService>();
 

        services.AddScoped<Kernel>(sp =>
        {
            var builder = Kernel.CreateBuilder();

            builder.AddOpenAIChatCompletion(
                modelId: config["OpenAI:Model"] ?? "openai/gpt-4.1-mini",
                apiKey: config["OpenAI:ApiKey"]!,
                 endpoint: new Uri("https://models.github.ai/inference"));

            builder.Plugins.AddFromObject(sp.GetRequiredService<WeatherPlugin>(), "Weather");
            builder.Plugins.AddFromObject(sp.GetRequiredService<SoilPlugin>(), "Soil");
            builder.Plugins.AddFromObject(sp.GetRequiredService<TaskHistoryPlugin>(), "TaskHistory");

            var kernel = builder.Build();

            
           
            return kernel;
        });
    }
}