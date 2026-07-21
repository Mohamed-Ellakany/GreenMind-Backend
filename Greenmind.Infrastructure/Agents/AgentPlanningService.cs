using Greenmind.Application.Dtos.AI;
using Greenmind.Application.Interfaces.AI;
using Greenmind.Infrastructure.Agents.Prompts;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using System.Text.Json;

namespace Greenmind.Infrastructure.Agents
{
    public class AgentPlanningService : IAgentPlanningService
    {
        private readonly Kernel _kernel;
        private readonly IFarmTaskPersistenceService _persistence;
        private readonly FarmPlanningPromptBuilder _promptBuilder;

        public AgentPlanningService(
            Kernel kernel,
            FarmPlanningPromptBuilder promptBuilder,
            IFarmTaskPersistenceService persistence)
        {
            _kernel = kernel;
            _persistence = persistence;
            _promptBuilder = promptBuilder;
        }

        public async Task<AgentResponse> GeneratePlanAsync(AgentRequest request)
        {
            var systemPrompt = _promptBuilder.BuildSystemPrompt(request);   
            var userPrompt = _promptBuilder.BuildUserPrompt(request);

            var executionSettings = new OpenAIPromptExecutionSettings
            {
                FunctionChoiceBehavior = FunctionChoiceBehavior.Auto(),
                Temperature = 0.2,
                ResponseFormat = "json_object"
            };

            var history = new ChatHistory();
            history.AddSystemMessage(systemPrompt);
            history.AddUserMessage(userPrompt);

            var chatService = _kernel.GetRequiredService<IChatCompletionService>();
            var result = await chatService.GetChatMessageContentAsync(history, executionSettings, _kernel);

            var json = result.Content ?? throw new InvalidOperationException("Empty model response");

            var response = JsonSerializer.Deserialize<AgentResponse>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            }) ?? new AgentResponse();

            response.Tasks = response.Tasks
                .Where(t => t.PlannedDate >= request.PlanningWindow.StartDate
                         && t.PlannedDate <= request.PlanningWindow.EndDate)
                .ToList();

            await _persistence.SaveGeneratedTasksAsync(request.ExportPlan.ExportPlanId, response);

            return response;
        }
    }
}