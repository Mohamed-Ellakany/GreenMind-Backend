using System;
using System.Collections.Generic;
using System.Text;

namespace Greenmind.Application.Dtos.AI
{
    public class ExportPlanDto
    {
        public int ExportPlanId { get; set; }  
        public string Crop { get; set; } = null!;        
        public string Destination { get; set; } = null!;
        public string Governorate { get; set; } = null!;
        public string City { get; set; } = null!;
        public decimal AreaInFeddan { get; set; }
        public DateOnly PlantingDate { get; set; }
        public DateOnly ExpectedHarvestDate { get; set; }
        public DateOnly ExpectedExportDate { get; set; }
        public int? CurrentSeasonDay { get; set; }
    }
}
