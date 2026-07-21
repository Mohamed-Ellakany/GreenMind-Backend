using Greenmind.Application.Interfaces.AI;
using Microsoft.SemanticKernel;
using System.ComponentModel;
using System.Text.Json;

namespace Greenmind.Infrastructure.Agents.Plugins
{
    public class SoilPlugin
    {
        private readonly ISoilMeasurementRepository _repo;
        public SoilPlugin(ISoilMeasurementRepository repo) => _repo = repo;

        [KernelFunction("get_latest_soil_measurement")]
        [Description("Gets the most recent user-submitted soil measurement (pH, EC, N, P, K, moisture) for an export plan. You must analyze these raw values yourself to decide if fertilization or irrigation adjustments are needed.")]
        public async Task<string> GetLatestAsync(
            [Description("Export plan id (integer)")] int exportPlanId)
        {
            var soil = await _repo.GetLatestAsync(exportPlanId);
            return soil is null ? "No soil data available" : JsonSerializer.Serialize(soil);
        }

        [KernelFunction("get_soil_history")]
        [Description("Gets soil measurement history since a given date, useful for spotting nutrient/pH/moisture trends over time.")]
        public async Task<string> GetHistoryAsync(
            [Description("Export plan id (integer)")] int exportPlanId,
            [Description("Since date yyyy-MM-dd")] string since)
        {
            var history = await _repo.GetHistoryAsync(exportPlanId, DateOnly.Parse(since));
            return JsonSerializer.Serialize(history);
        }
    }
}
