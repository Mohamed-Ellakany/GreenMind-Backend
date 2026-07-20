using System;
using System.Collections.Generic;
using System.Text;

namespace Greenmind.Domain.Entities
{
    public class SoilMeasurement : BaseEntity
    {
        public int ExportPlanId { get; set; }

        public DateOnly MeasurementDate { get; set; }

        public double PH { get; set; }

        public double EC { get; set; }

        public double Nitrogen { get; set; }

        public double Phosphorus { get; set; }

        public double Potassium { get; set; }

        public double Moisture { get; set; }

        public string? Notes { get; set; }

        // Navigation

        public ExportPlan ExportPlan { get; set; } = null!;
    }
}
