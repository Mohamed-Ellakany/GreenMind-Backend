using Greenmind.Domain.Consts.Enums;
using System;
using System.Collections.Generic;
using System.Text;

namespace Greenmind.Domain.Entities
{
    public class ExportPlan : BaseEntity
    {
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

        // Navigation

        public Farm Farm { get; set; } = null!;

        public Crop Crop { get; set; } = null!;

        public Destination Destination { get; set; } = null!;

        public ICollection<Task> Tasks { get; set; } = new List<Task>();

        public ICollection<SoilMeasurement> SoilMeasurements { get; set; } = new List<SoilMeasurement>();
    }
}
