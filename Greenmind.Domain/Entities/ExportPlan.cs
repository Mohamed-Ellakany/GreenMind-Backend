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
        public ICollection<DiseaseReport> DiseaseReports { get; set; } = new List<DiseaseReport>();
        public ExportPlanBudget? Budget { get; set; }
        public ICollection<Expense> Expenses { get; set; } = new List<Expense>();

    }
}
