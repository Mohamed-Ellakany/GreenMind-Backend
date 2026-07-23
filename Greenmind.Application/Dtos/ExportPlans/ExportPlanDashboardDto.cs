namespace Greenmind.Application.Dtos.ExportPlans
{
    public class ExportPlanDashboardDto
    {
        public int ExportPlanId { get; set; }

        public required string Name { get; set; }

        public required string CropName { get; set; }

        public required string DestinationName { get; set; }

        public DateOnly PlantingDate { get; set; }

        public DateOnly ExpectedHarvestDate { get; set; }

        // Dynamic Calculations (Non-persisted)
        public int CurrentSeasonDay { get; set; }

        public double CurrentSeasonProgress { get; set; }

        public required string GrowthStage { get; set; }
    }
}
