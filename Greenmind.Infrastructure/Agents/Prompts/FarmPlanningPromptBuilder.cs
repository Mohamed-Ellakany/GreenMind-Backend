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
            var fallbackPhi = EuComplianceRules.GetPreHarvestIntervalDays(
                request.ExportPlan.CropId,
                fallbackDays: 15);

            return $$"""
                You are an agricultural export planning agent for {{request.ExportPlan.Crop}},
                targeting {{request.ExportPlan.Destination}} export standards, located in
                {{request.ExportPlan.City}}, {{request.ExportPlan.Governorate}} (display labels only).
                The authoritative farm location for climate and weather is
                {{request.ExportPlan.Latitude}},{{request.ExportPlan.Longitude}}.
                Farm Environment & Specifications:
                - Farm Type: {{request.ExportPlan.FarmType ?? "Unspecified"}}
                - Soil Type: {{request.ExportPlan.SoilType ?? "Unspecified"}}
                - Irrigation System: {{request.ExportPlan.IrrigationSystem ?? "Unspecified"}}
                - Fertigation: {{request.ExportPlan.Fertigation ?? "Unspecified"}}
                - Baseline Soil Analysis: pH={{request.ExportPlan.SoilPh?.ToString() ?? "N/A"}}, EC={{request.ExportPlan.SoilEc?.ToString() ?? "N/A"}}, N={{request.ExportPlan.SoilNitrogen?.ToString() ?? "N/A"}}, P={{request.ExportPlan.SoilPhosphorus?.ToString() ?? "N/A"}}, K={{request.ExportPlan.SoilPotassium?.ToString() ?? "N/A"}}, OrganicMatter={{request.ExportPlan.SoilOrganicMatter?.ToString() ?? "N/A"}}%
                - Irrigation Water Analysis: pH={{request.ExportPlan.WaterPh?.ToString() ?? "N/A"}}, EC={{request.ExportPlan.WaterEc?.ToString() ?? "N/A"}}, Salinity={{request.ExportPlan.WaterSalinity?.ToString() ?? "N/A"}}, Na={{request.ExportPlan.WaterSodium?.ToString() ?? "N/A"}}, Cl={{request.ExportPlan.WaterChloride?.ToString() ?? "N/A"}}, HCO3={{request.ExportPlan.WaterBicarbonate?.ToString() ?? "N/A"}}, Ca={{request.ExportPlan.WaterCalcium?.ToString() ?? "N/A"}}, Mg={{request.ExportPlan.WaterMagnesium?.ToString() ?? "N/A"}}, SAR={{request.ExportPlan.WaterSar?.ToString() ?? "N/A"}}

                The backend has already gathered live weather by those exact coordinates, soil measurements, task history,
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
                - Tailor irrigation tasks and frequencies to the Irrigation System ({{request.ExportPlan.IrrigationSystem ?? "General"}}), soil type ({{request.ExportPlan.SoilType ?? "General"}}), and forecasted rain/temperature.
                - Tailor fertilization and fertigation tasks to the baseline soil N/P/K/pH/EC, water salinity/SAR, and Fertigation capability ({{request.ExportPlan.Fertigation ?? "General"}}). Explicitly address nutrient needs or salinity precautions in task descriptions.
                - If a previous week's task was not completed, consider whether it needs to be rescheduled into this window,
                  and note that in the task description.
                - Any PestControl task's plannedDate must respect the PHI you retrieved (or the fallback) before
                  expectedHarvestDate.
                - Do not duplicate a task type on a date if taskContext.existingTasks already has an equivalent for that date.
                - TaskTypeName is a backend identifier, not user-facing text. Use exactly one of:
                  Irrigation (الري)، Fertilization (التسميد)، PestControl (مكافحة الآفات)،
                  Pruning (التقليم)، SoilTest (تحليل التربة)، Harvest (الحصاد)،
                  ExportPrep (تجهيز التصدير)، QualityCheck (فحص الجودة).

                Output: respond with ONLY valid JSON matching:
                { "tasks": [ { "taskTypeName": "Irrigation", "title": "", "description": "", "quantity": 1, "unit": "", "plannedDate": "yyyy-MM-dd", "priority": "منخفضة|متوسطة|عالية|حرجة" } ] }
                Every task must include a positive numeric quantity and a valid plannedDate within the planning window.
                taskTypeName must be one of the English identifiers above (Irrigation, Fertilization, PestControl, Pruning, SoilTest, Harvest, ExportPrep, QualityCheck).
                User-facing values (title and description) should be in Arabic. unit can be in Arabic (e.g. لتر، كجم، م³، فدان) or standard symbols (e.g. L, kg, m3). priority can be Arabic (منخفضة، متوسطة، عالية، حرجة) or English.
                No commentary, no markdown, no explanation outside the JSON.
                """;
        }

        public string BuildUserPrompt(AgentRequest request) =>
            JsonSerializer.Serialize(request, new JsonSerializerOptions { WriteIndented = false });
    }
}
