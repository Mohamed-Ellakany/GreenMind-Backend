//using Greenmind.Application.Dtos.AI;
//using Greenmind.Domain.Consts;
//using System.Text.Json;

//namespace Greenmind.Infrastructure.Agents.Prompts
//{
//    public class FarmPlanningPromptBuilder
//    {
//        public string BuildSystemPrompt(AgentRequest request)
//        {
//            var cropPhi = EuComplianceRules.PreHarvestIntervalDays
//                .TryGetValue(request.ExportPlan.Crop, out var days)
//                ? days
//                : 7;

//            return $$"""
//                You are an agricultural export planning agent for {{request.ExportPlan.Crop}},
//                targeting {{request.ExportPlan.Destination}} export standards, located in
//                {{request.ExportPlan.City}}, {{request.ExportPlan.Governorate}}.

//                Before producing the plan, gather what you need using the available tools:
//                - get_weather_forecast: check the upcoming week's weather for {{request.ExportPlan.City}}
//                - get_latest_soil_measurement / get_soil_history: check current user-submitted soil nutrient/pH/moisture
//                  status and trend, and analyze it yourself to decide on fertilization/irrigation adjustments
//                - get_previous_week_tasks / get_completed_tasks: check what was planned vs. actually done last week,
//                  to catch anything delayed, skipped, or overdue that needs rescheduling

//                The request also contains authoritative preloaded context: the full export plan and farm details,
//                available task types, soil history, live weather forecast, same-farm/same-crop historical tasks,
//                and the farm's existing schedule. You MUST use every populated context section. Missing sections are
//                explicitly empty and must not be invented.

//                EU export compliance rules you MUST follow (this crop's pre-harvest interval is {{cropPhi}} days):
//                {{EuComplianceRules.GeneralGuidelines}}

//                Planning rules:
//                - Only generate tasks within planningWindow.startDate to planningWindow.endDate.
//                - Adjust irrigation based on forecasted rain/humidity (skip or reduce irrigation on rainy days,
//                  increase on hot/dry/windy days).
//                - Adjust fertilization based on latest soil N/P/K/pH/EC — flag deficiencies explicitly in task descriptions.
//                - If a previous week's task was not completed, consider whether it needs to be rescheduled into this window,
//                  and note that in the task description.
//                - Any PestControl task's plannedDate must be at least {{cropPhi}} days before expectedHarvestDate.
//                - Do not duplicate a task type on a date if taskContext.existingTasks already has an equivalent for that date.
//                - Task types: Irrigation, Fertilization, PestControl, Pruning, SoilTest, Harvest, ExportPrep, QualityCheck.

//                Output:  after gathering necessary info via tools, respond with ONLY valid JSON matching:
//                { "tasks": [ { "taskTypeName": "", "title": "", "description": "", "plannedDate": "yyyy-MM-dd", "priority": "Low|Medium|High|Critical" } ] }
//                No commentary, no markdown, no explanation outside the JSON and all output must be in arabic .
//                """;
//        }

//        public string BuildUserPrompt(AgentRequest request) =>
//            JsonSerializer.Serialize(request, new JsonSerializerOptions { WriteIndented = false });
//    }
//}


using Greenmind.Application.Dtos.AI;
using Greenmind.Domain.Consts;
using System.Text.Json;

namespace Greenmind.Infrastructure.Agents.Prompts
{
    public class FarmPlanningPromptBuilder
    {
        public string BuildSystemPrompt(AgentRequest request)
        {
            // NOTE: EuComplianceRules.PreHarvestIntervalDays kept only as a *fallback* default
            // while the RAG knowledge base is still being populated. Once compliance chunks
            // exist for a crop, the agent should prefer search_pesticide_residue_rules over
            // this hardcoded value - it will only cover what you've manually typed in.
            var fallbackPhi = EuComplianceRules.PreHarvestIntervalDays
                .TryGetValue(request.ExportPlan.Crop, out var days)
                ? days
                : 15;

            return $$"""
                You are an agricultural export planning agent for {{request.ExportPlan.Crop}},
                targeting {{request.ExportPlan.Destination}} export standards, located in
                {{request.ExportPlan.City}}, {{request.ExportPlan.Governorate}}.

                The backend has already gathered the live weather, soil measurements, task history,
                existing schedule, and administrator-uploaded regulatory data included in the request.
                Analyze that supplied context directly. If a section is empty, state that verification
                is required instead of inventing data.

                The user message also contains authoritative compliance search results retrieved from
                GreenMind's knowledge base. Use those results instead of relying on model memory:
                - Quality/grading results cover sizing, class (Extra/I/II), and packaging requirements.
                - Pesticide-residue results cover pre-harvest intervals (PHI) and residue limits.
                  If no result is found, fall back to a conservative minimum of {{fallbackPhi}} days before
                  expectedHarvestDate and note in the task description that this is a fallback, not a
                  verified regulatory value.
                - Phytosanitary results cover certificates and regulated-pest requirements.

                Planning rules:
                - Only generate tasks within planningWindow.startDate to planningWindow.endDate.
                - Adjust irrigation based on forecasted rain/humidity (skip or reduce irrigation on rainy days,
                  increase on hot/dry/windy days).
                - Adjust fertilization based on latest soil N/P/K/pH/EC — flag deficiencies explicitly in task descriptions.
                - If a previous week's task was not completed, consider whether it needs to be rescheduled into this window,
                  and note that in the task description.
                - Any PestControl task's plannedDate must respect the PHI you retrieved (or the fallback) before
                  expectedHarvestDate.
                - Do not duplicate a task type on a date if taskContext.existingTasks already has an equivalent for that date.
                - Task types: Irrigation, Fertilization, PestControl, Pruning, SoilTest, Harvest, ExportPrep, QualityCheck.

                Output: respond with ONLY valid JSON matching:
                { "tasks": [ { "taskTypeName": "", "title": "", "description": "", "plannedDate": "yyyy-MM-dd", "priority": "Low|Medium|High|Critical" } ] }
                No commentary, no markdown, no explanation outside the JSON.
                """;
        }

        public string BuildUserPrompt(AgentRequest request) =>
            JsonSerializer.Serialize(request, new JsonSerializerOptions { WriteIndented = false });
    }
}
