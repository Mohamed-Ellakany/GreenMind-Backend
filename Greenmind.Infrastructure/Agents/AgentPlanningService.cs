using Greenmind.Application.Dtos.AI;
using Greenmind.Application.Interfaces.AI;
using Greenmind.Infrastructure.Agents.Plugins;
using Greenmind.Infrastructure.Agents.Prompts;
using Greenmind.Infrastructure.Iti;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Greenmind.Infrastructure.Agents
{
    public class AgentPlanningService : IAgentPlanningService
    {
        private readonly IItiBedrockGatewayClient _gatewayClient;
        private readonly ComplianceRagPlugin _complianceRag;
        private readonly FarmPlanningPromptBuilder _promptBuilder;

        public AgentPlanningService(
            IItiBedrockGatewayClient gatewayClient,
            ComplianceRagPlugin complianceRag,
            FarmPlanningPromptBuilder promptBuilder)
        {
            _gatewayClient = gatewayClient;
            _complianceRag = complianceRag;
            _promptBuilder = promptBuilder;
        }

        public async Task<AgentResponse> GeneratePlanAsync(AgentRequest request)
        {
            var systemPrompt = _promptBuilder.BuildSystemPrompt(request);   
            var userPrompt = _promptBuilder.BuildUserPrompt(request);
            var complianceContext = await BuildComplianceContextAsync(request);
            var response = await GenerateAsync(systemPrompt, userPrompt, complianceContext);
            if (response.Tasks.Any(HasNonArabicUserText))
            {
                response = await GenerateAsync(
                    systemPrompt + "\nكل قيم title وdescription وunit داخل tasks يجب أن تكون عربية بالكامل، مع الإبقاء على taskTypeName كما هو.",
                    userPrompt,
                    complianceContext);
            }

            response.Tasks = response.Tasks
                .Where(t => t.PlannedDate >= request.PlanningWindow.StartDate
                         && t.PlannedDate <= request.PlanningWindow.EndDate)
                .Where(t => !HasNonArabicUserText(t))
                .ToList();

            return response;
        }

        private async Task<AgentResponse> GenerateAsync(
            string systemPrompt,
            string userPrompt,
            string complianceContext)
        {
            var json = await _gatewayClient.ChatAsync(
                systemPrompt,
                [new ItiChatMessage(
                    "user",
                    $"{userPrompt}\n\nAuthoritative compliance search results:\n{complianceContext}")],
                modelId: ItiBedrockGatewayClient.DefaultStructuredModel);

            return JsonSerializer.Deserialize<AgentResponse>(ItiJson.ExtractObject(json), new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                NumberHandling = JsonNumberHandling.AllowReadingFromString,
                Converters = { new JsonStringEnumConverter() }
            }) ?? new AgentResponse();
        }

        private static bool HasNonArabicUserText(TaskItemDto task) =>
            !ContainsArabicLetter(task.Title) ||
            !ContainsArabicLetter(task.Unit) ||
            (!string.IsNullOrWhiteSpace(task.Description) &&
                !ContainsArabicLetter(task.Description));

        private static bool ContainsArabicLetter(string? value) =>
            !string.IsNullOrWhiteSpace(value) &&
            value.Any(character => character is >= '\u0600' and <= '\u06ff');

        private async Task<string> BuildComplianceContextAsync(AgentRequest request)
        {
            var crop = request.ExportPlan.Crop;
            var quality = await _complianceRag.SearchQualityGradingAsync(
                $"quality grading sizing packaging requirements for {crop}", crop);
            var phytosanitary = await _complianceRag.SearchPhytosanitaryAsync(
                $"phytosanitary export requirements for {crop} entering {request.ExportPlan.Destination}", crop);
            var pesticide = await _complianceRag.SearchPesticideResidueAsync(
                $"pesticide residue limits and pre-harvest intervals for {crop}", crop);

            return $"Quality/grading:\n{quality}\nPhytosanitary:\n{phytosanitary}\nPesticide residue:\n{pesticide}";
        }
    }
}
