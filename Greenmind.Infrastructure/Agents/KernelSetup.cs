using Greenmind.Application.Contracts.Persistence;
using Greenmind.Application.Interfaces.AI;
using Greenmind.Infrastructure.Agents.Plugins;
using Greenmind.Infrastructure.Agents.Prompts;
using Greenmind.Infrastructure.Persistence;
using Greenmind.Infrastructure.Persistence.Repositories;
using Greenmind.Infrastructure.Rag;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.SemanticKernel;
using OpenAI;
using OpenAI.Embeddings;
using System.ClientModel;

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

        // --- NEW: RAG registrations ---------------------------------------------------
        // IMPORTANT: this is registered on the app's OWN service collection, NOT inside the
        // Kernel.CreateBuilder() factory below. Kernel.CreateBuilder() creates a completely
        // separate, isolated internal service container - anything registered only on
        // "builder.Services"/"builder.AddXyz(...)" inside that factory is invisible to the
        // rest of the app's DI container. Since ComplianceIngestionService and
        // ComplianceRagPlugin are resolved from the APP's container (via AddScoped below,
        // and via sp.GetRequiredService<ComplianceRagPlugin>() inside the Kernel factory),
        // the embedding generator must be registered here, at the app level, instead.
        //
        // AsIEmbeddingGenerator() is a stable (non-experimental) extension from the
        // Microsoft.Extensions.AI.OpenAI package - no SKEXP pragma needed here.
        services.AddSingleton<IEmbeddingGenerator<string, Embedding<float>>>(_ =>
        {
            var openAiClient = new OpenAIClient(
                new ApiKeyCredential(config["OpenAI:ApiKey"]!),
                new OpenAIClientOptions { Endpoint = new Uri("https://models.github.ai/inference") });

            var embeddingClient = openAiClient.GetEmbeddingClient(
                config["Embeddings:Model"] ?? "openai/text-embedding-3-small");

            return embeddingClient.AsIEmbeddingGenerator();
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
            builder.Plugins.AddFromObject(sp.GetRequiredService<RegulatoryDataPlugin>(), "RegulatoryData");

            // --- NEW: register the RAG plugin so the agent can call it ------------------
            // ComplianceRagPlugin was resolved from the app's own container (sp), so its
            // constructor dependency on IEmbeddingGenerator<...> above was already satisfied
            // there - nothing extra needed on "builder" for this to work.
            builder.Plugins.AddFromObject(sp.GetRequiredService<ComplianceRagPlugin>(), "ComplianceRag");
            // ---------------------------------------------------------------------------------

            var kernel = builder.Build();

            return kernel;
        });
    }
}