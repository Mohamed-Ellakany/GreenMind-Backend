using Greenmind.Domain.Consts.Enums;

namespace Greenmind.Application.Dtos.ExportPlans
{
    public class ExportPlanSummaryDto
    {
        public int Id { get; set; }

        public required string Name { get; set; }

        public ExportPlanStatus Status { get; set; }

        public DateOnly PlantingDate { get; set; }

        public DateOnly ExpectedHarvestDate { get; set; }

        // Dynamic Calculations (Non-persisted)
        public int CurrentSeasonDay { get; set; }

        public double CurrentSeasonProgress { get; set; }

        public required string GrowthStage { get; set; }
    }
}
