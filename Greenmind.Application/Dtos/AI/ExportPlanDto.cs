using System;
using System.Collections.Generic;
using System.Text;

namespace Greenmind.Application.Dtos.AI
{
    public class ExportPlanDto
    {
        public int ExportPlanId { get; set; }  
        public string PlanName { get; set; } = null!;
        public string FarmName { get; set; } = null!;
        public int CropId { get; set; }
        public string Crop { get; set; } = null!;        
        public string? CropDescription { get; set; }
        public string Destination { get; set; } = null!;
        public string? DestinationDescription { get; set; }
        public string Governorate { get; set; } = null!;
        public string City { get; set; } = null!;
        public double Latitude { get; set; }
        public double Longitude { get; set; }
        public decimal AreaInFeddan { get; set; }
        public DateOnly PlantingDate { get; set; }
        public DateOnly ExpectedHarvestDate { get; set; }
        public DateOnly ExpectedExportDate { get; set; }
        public int? CurrentSeasonDay { get; set; }
        public string? GrowthStage { get; set; }
        public string? Notes { get; set; }
        public string? FarmType { get; set; }
        public string? SoilType { get; set; }
        public string? IrrigationSystem { get; set; }
        public string? Fertigation { get; set; }
        public double? SoilPh { get; set; }
        public double? SoilEc { get; set; }
        public double? SoilNitrogen { get; set; }
        public double? SoilPhosphorus { get; set; }
        public double? SoilPotassium { get; set; }
        public double? SoilOrganicMatter { get; set; }
        public double? WaterPh { get; set; }
        public double? WaterEc { get; set; }
        public double? WaterSalinity { get; set; }
        public double? WaterSodium { get; set; }
        public double? WaterChloride { get; set; }
        public double? WaterBicarbonate { get; set; }
        public double? WaterCalcium { get; set; }
        public double? WaterMagnesium { get; set; }
        public double? WaterSar { get; set; }
    }
}
