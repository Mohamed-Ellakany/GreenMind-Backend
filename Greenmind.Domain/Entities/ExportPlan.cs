using Greenmind.Domain.Consts.Enums;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text;

namespace Greenmind.Domain.Entities
{
    public class ExportPlan : BaseEntity
    {
        [Required]
        [ForeignKey(nameof(Farm))]
        public int FarmId { get; set; }

        [Required]
        [ForeignKey(nameof(Crop))]
        public int CropId { get; set; }

        [Required]
        [ForeignKey(nameof(Destination))]
        public int DestinationId { get; set; }

        [Required]
        [MaxLength(200)]
        public required string Name { get; set; }

        [Required]
        [MaxLength(100)]
        public required string Governorate { get; set; }

        [Required]
        [MaxLength(100)]
        public required string City { get; set; }

        [Column(TypeName = "float")]
        public double Latitude { get; set; }

        [Column(TypeName = "float")]
        public double Longitude { get; set; }

        [Column(TypeName = "decimal(10,2)")]
        public decimal AreaInFeddan { get; set; }

        public DateOnly PlantingDate { get; set; }

        public DateOnly ExpectedHarvestDate { get; set; }

        public DateOnly ExpectedExportDate { get; set; }

        [Required]
        public ExportPlanStatus Status { get; set; }

        [MaxLength(1000)]
        public string? Notes { get; set; }

        // Farm Environment & Characteristics
        [MaxLength(50)]
        public string? FarmType { get; set; }

        [MaxLength(50)]
        public string? SoilType { get; set; }

        // Soil Analysis (Manual Input)
        [Column(TypeName = "float")]
        public double? SoilPh { get; set; }

        [Column(TypeName = "float")]
        public double? SoilEc { get; set; }

        [Column(TypeName = "float")]
        public double? SoilNitrogen { get; set; }

        [Column(TypeName = "float")]
        public double? SoilPhosphorus { get; set; }

        [Column(TypeName = "float")]
        public double? SoilPotassium { get; set; }

        [Column(TypeName = "float")]
        public double? SoilOrganicMatter { get; set; }

        // Irrigation Water Analysis (Manual Input)
        [Column(TypeName = "float")]
        public double? WaterPh { get; set; }

        [Column(TypeName = "float")]
        public double? WaterEc { get; set; }

        [Column(TypeName = "float")]
        public double? WaterSalinity { get; set; }

        [Column(TypeName = "float")]
        public double? WaterSodium { get; set; }

        [Column(TypeName = "float")]
        public double? WaterChloride { get; set; }

        [Column(TypeName = "float")]
        public double? WaterBicarbonate { get; set; }

        [Column(TypeName = "float")]
        public double? WaterCalcium { get; set; }

        [Column(TypeName = "float")]
        public double? WaterMagnesium { get; set; }

        [Column(TypeName = "float")]
        public double? WaterSar { get; set; }

        // Irrigation System
        [MaxLength(50)]
        public string? IrrigationSystem { get; set; }

        [MaxLength(50)]
        public string? Fertigation { get; set; }

        public bool IsSoilAnalysisCompleted() =>
            SoilPh.HasValue && SoilPh.Value >= 0 && SoilPh.Value <= 14 &&
            SoilEc.HasValue && SoilEc.Value >= 0 &&
            SoilNitrogen.HasValue && SoilNitrogen.Value >= 0 &&
            SoilPhosphorus.HasValue && SoilPhosphorus.Value >= 0 &&
            SoilPotassium.HasValue && SoilPotassium.Value >= 0 &&
            SoilOrganicMatter.HasValue && SoilOrganicMatter.Value >= 0;

        public bool IsWaterAnalysisCompleted() =>
            WaterPh.HasValue && WaterPh.Value >= 0 && WaterPh.Value <= 14 &&
            WaterEc.HasValue && WaterEc.Value >= 0 &&
            WaterSalinity.HasValue && WaterSalinity.Value >= 0 &&
            WaterSodium.HasValue && WaterSodium.Value >= 0 &&
            WaterChloride.HasValue && WaterChloride.Value >= 0 &&
            WaterBicarbonate.HasValue && WaterBicarbonate.Value >= 0 &&
            WaterCalcium.HasValue && WaterCalcium.Value >= 0 &&
            WaterMagnesium.HasValue && WaterMagnesium.Value >= 0 &&
            WaterSar.HasValue && WaterSar.Value >= 0;

        public bool IsFarmDataReady() =>
            !string.IsNullOrWhiteSpace(FarmType) &&
            !string.IsNullOrWhiteSpace(SoilType) &&
            !string.IsNullOrWhiteSpace(IrrigationSystem) &&
            IsSoilAnalysisCompleted() &&
            IsWaterAnalysisCompleted();

        [Required]
        public PlanningGenerationStatus PlanningStatus { get; set; } = PlanningGenerationStatus.Pending;

        public DateTime? PlanningRequestedAtUtc { get; set; }
        public DateTime? PlanningStartedAtUtc { get; set; }
        public DateTime? PlanningCompletedAtUtc { get; set; }
        public DateTime? NextPlanningAttemptAtUtc { get; set; }
        public int PlanningAttemptCount { get; set; }
        public int GeneratedTaskCount { get; set; }

        [MaxLength(500)]
        public string? PlanningFailureMessage { get; set; }

        // Navigation
        public Farm Farm { get; set; } = null!;
        public Crop Crop { get; set; } = null!;
        public Destination Destination { get; set; } = null!;

        public ICollection<FarmTask> FarmTasks { get; set; } = new List<FarmTask>();
        public ICollection<SoilMeasurement> SoilMeasurements { get; set; } = new List<SoilMeasurement>();
        public ICollection<ChatSession> ChatSessions { get; set; } = new List<ChatSession>();
        public ICollection<TaskChangeProposal> TaskChangeProposals { get; set; } = new List<TaskChangeProposal>();
        public ICollection<ScheduleRevision> ScheduleRevisions { get; set; } = new List<ScheduleRevision>();
        public ICollection<DiseaseReport> DiseaseReports { get; set; } = new List<DiseaseReport>();
        public ExportPlanBudget? Budget { get; set; }
        public ICollection<Expense> Expenses { get; set; } = new List<Expense>();

    }
}
