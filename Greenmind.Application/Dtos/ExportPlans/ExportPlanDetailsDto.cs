using Greenmind.Domain.Consts.Enums;

namespace Greenmind.Application.Dtos.ExportPlans
{
    public class ExportPlanDetailsDto
    {
        public int Id { get; set; }

        public int FarmId { get; set; }

        public int CropId { get; set; }

        public int DestinationId { get; set; }

        public required string Name { get; set; }

        public required string Governorate { get; set; }

        public required string City { get; set; }

        public double Latitude { get; set; }

        public double Longitude { get; set; }

        public decimal AreaInFeddan { get; set; }

        public DateOnly PlantingDate { get; set; }

        public DateOnly ExpectedHarvestDate { get; set; }

        public DateOnly ExpectedExportDate { get; set; }

        public ExportPlanStatus Status { get; set; }

        public string? Notes { get; set; }

        // Dynamic Calculations (Non-persisted)
        public int CurrentSeasonDay { get; set; }

        public double CurrentSeasonProgress { get; set; }

        public required string GrowthStage { get; set; }
    }
}
