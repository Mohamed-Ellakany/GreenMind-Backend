using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text;

namespace Greenmind.Domain.Entities
{
    public class SoilMeasurement : BaseEntity
    {
        [Required]
        [ForeignKey(nameof(ExportPlan))]
        public int ExportPlanId { get; set; }

        public DateOnly MeasurementDate { get; set; }

        [Column(TypeName = "float")]
        public double PH { get; set; }

        [Column(TypeName = "float")]
        public double EC { get; set; }

        [Column(TypeName = "float")]
        public double Nitrogen { get; set; }

        [Column(TypeName = "float")]
        public double Phosphorus { get; set; }

        [Column(TypeName = "float")]
        public double Potassium { get; set; }

        [Column(TypeName = "float")]
        public double Moisture { get; set; }

        [MaxLength(500)]
        public string? Notes { get; set; }

        // Navigation
        public ExportPlan ExportPlan { get; set; } = null!;
    }
}
