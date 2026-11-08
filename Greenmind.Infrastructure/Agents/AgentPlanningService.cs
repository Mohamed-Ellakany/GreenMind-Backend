using Greenmind.Application.Dtos.AI;
using Greenmind.Application.Interfaces.AI;
using Greenmind.Infrastructure.Agents.Plugins;
using Greenmind.Infrastructure.Agents.Prompts;
using Greenmind.Infrastructure.Iti;
using Greenmind.Domain.Consts;
using Greenmind.Domain.Consts.Enums;
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
            var response = await GenerateAsync(systemPrompt, userPrompt, complianceContext, request.ExportPlan.ExportPlanId);
            response.Tasks ??= [];

            SanitizeTasks(response.Tasks, request);

            if (response.Tasks.Count == 0)
            {
                response = await GenerateAsync(
                    systemPrompt + "\nقم بتوليد مهام زراعية محددة ومفصلة باللغة العربية مع تحديد الكميات والوحدات والتواريخ بوضوح.",
                    userPrompt,
                    complianceContext,
                    request.ExportPlan.ExportPlanId);
                response.Tasks ??= [];
                SanitizeTasks(response.Tasks, request);
            }

            // Ensure task planned dates fall within the planning window
            var start = request.PlanningWindow.StartDate;
            var end = request.PlanningWindow.EndDate;

            foreach (var task in response.Tasks)
            {
                if (task.PlannedDate < start || task.PlannedDate > end)
                {
                    task.PlannedDate = start;
                }
            }

            response.Tasks = response.Tasks
                .Where(t => t.PlannedDate >= start && t.PlannedDate <= end)
                .Where(t => !string.IsNullOrWhiteSpace(t.TaskTypeName))
                .ToList();

            return response;
        }

        private async Task<AgentResponse> GenerateAsync(
            string systemPrompt,
            string userPrompt,
            string complianceContext,
            int exportPlanId)
        {
            var json = await _gatewayClient.ChatAsync(
                systemPrompt,
                [new ItiChatMessage(
                    "user",
                    $"{userPrompt}\n\nAuthoritative compliance search results:\n{complianceContext}")],
                modelId: ItiBedrockGatewayClient.DefaultStructuredModel,
                usageContext: new ItiAiRequestContext(
                    AiFeatureNames.ExportPlanTaskGeneration,
                    ExportPlanId: exportPlanId));

            return JsonSerializer.Deserialize<AgentResponse>(ItiJson.ExtractObject(json), new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                NumberHandling = JsonNumberHandling.AllowReadingFromString,
                Converters =
                {
                    new ArabicTaskPriorityJsonConverter(),
                    new JsonStringEnumConverter(),
                    new FlexibleDateOnlyJsonConverter(),
                    new FlexibleStringJsonConverter()
                }
            }) ?? new AgentResponse();
        }

        private static void SanitizeTasks(List<TaskItemDto> tasks, AgentRequest request)
        {
            var crop = request.ExportPlan.Crop;
            foreach (var task in tasks)
            {
                task.TaskTypeName = TaskTypeNames.Normalize(task.TaskTypeName);
                if (task.Quantity <= 0)
                {
                    task.Quantity = 1;
                }

                if (string.IsNullOrWhiteSpace(task.Unit))
                {
                    task.Unit = GetDefaultUnitForTaskType(task.TaskTypeName);
                }

                if (string.IsNullOrWhiteSpace(task.Title))
                {
                    task.Title = GetDefaultTitleForTaskType(task.TaskTypeName, crop);
                }

                if (string.IsNullOrWhiteSpace(task.Description))
                {
                    task.Description = task.Title;
                }
            }
        }

        private static string GetDefaultUnitForTaskType(string taskTypeName) => taskTypeName switch
        {
            TaskTypeNames.Irrigation => "م³",
            TaskTypeNames.Fertilization => "كجم",
            TaskTypeNames.PestControl => "لتر",
            TaskTypeNames.Pruning => "فدان",
            TaskTypeNames.SoilTest => "عينة",
            TaskTypeNames.Harvest => "طن",
            TaskTypeNames.ExportPrep => "صندوق",
            TaskTypeNames.QualityCheck => "فحص",
            _ => "وحدة"
        };

        private static string GetDefaultTitleForTaskType(string taskTypeName, string crop) => taskTypeName switch
        {
            TaskTypeNames.Irrigation => $"ري {crop}",
            TaskTypeNames.Fertilization => $"تسميد {crop}",
            TaskTypeNames.PestControl => $"مكافحة وقائية لـ {crop}",
            TaskTypeNames.Pruning => $"تقليم {crop}",
            TaskTypeNames.SoilTest => "تحليل التربة والمياه",
            TaskTypeNames.Harvest => $"حصاد {crop}",
            TaskTypeNames.ExportPrep => $"فرز وتجهيز {crop} للتصدير",
            TaskTypeNames.QualityCheck => $"فحص جودة {crop}",
            _ => $"مهمة زراعية لـ {crop}"
        };

        private sealed class ArabicTaskPriorityJsonConverter : JsonConverter<TaskPriority>
        {
            public override TaskPriority Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
            {
                if (reader.TokenType == JsonTokenType.Number && reader.TryGetInt32(out var numericValue) &&
                    Enum.IsDefined(typeof(TaskPriority), numericValue))
                {
                    return (TaskPriority)numericValue;
                }

                if (reader.TokenType == JsonTokenType.String)
                {
                    var value = reader.GetString()?.Trim();
                    if (string.IsNullOrWhiteSpace(value))
                        return TaskPriority.Medium;

                    var lower = value.ToLowerInvariant();
                    if (lower.Contains("منخفض") || lower.Contains("low") || lower.Contains("بسيط"))
                        return TaskPriority.Low;
                    if (lower.Contains("متوسط") || lower.Contains("عادي") || lower.Contains("medium") || lower.Contains("normal"))
                        return TaskPriority.Medium;
                    if (lower.Contains("عال") || lower.Contains("مرتفع") || lower.Contains("high"))
                        return TaskPriority.High;
                    if (lower.Contains("حرج") || lower.Contains("طارئ") || lower.Contains("critical") || lower.Contains("urgent"))
                        return TaskPriority.Critical;

                    if (Enum.TryParse<TaskPriority>(value, true, out var englishPriority))
                        return englishPriority;
                }

                return TaskPriority.Medium;
            }

            public override void Write(Utf8JsonWriter writer, TaskPriority value, JsonSerializerOptions options) =>
                writer.WriteStringValue(value switch
                {
                    TaskPriority.Low => "منخفضة",
                    TaskPriority.Medium => "متوسطة",
                    TaskPriority.High => "عالية",
                    TaskPriority.Critical => "حرجة",
                    _ => "متوسطة"
                });
        }

        private async Task<string> BuildComplianceContextAsync(AgentRequest request)
        {
            var crop = request.ExportPlan.Crop;
            var cropKnowledgeKey = CropCatalog.GetCanonicalKey(request.ExportPlan.CropId)
                ?? CropCatalog.NormalizeCanonicalKey(crop)
                ?? crop;
            var quality = await _complianceRag.SearchQualityGradingAsync(
                $"quality grading sizing packaging requirements for {crop}", cropKnowledgeKey);
            var phytosanitary = await _complianceRag.SearchPhytosanitaryAsync(
                $"phytosanitary export requirements for {crop} entering {request.ExportPlan.Destination}", cropKnowledgeKey);
            var pesticide = await _complianceRag.SearchPesticideResidueAsync(
                $"pesticide residue limits and pre-harvest intervals for {crop}", cropKnowledgeKey);

            return $"Quality/grading:\n{quality}\nPhytosanitary:\n{phytosanitary}\nPesticide residue:\n{pesticide}";
        }
    }
}
