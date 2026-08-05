using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text;

namespace Greenmind.Domain.Entities
{
    public class Farm : BaseEntity
    {
        [Required]
        [ForeignKey(nameof(Owner))]
        public required string OwnerUserId { get; set; }

        [Required]
        [MaxLength(200)]
        public required string Name { get; set; }

        public bool IsApproved { get; set; }

        public DateTime? ApprovedAtUtc { get; set; }

        // Navigation
        public ApplicationUser Owner { get; set; } = null!;

        public ICollection<ExportPlan> ExportPlans { get; set; } = new List<ExportPlan>();
        public ICollection<ChatSession> ChatSessions { get; set; } = new List<ChatSession>();
        public ICollection<SupportConversation> SupportConversations { get; set; } = new List<SupportConversation>();
        public ICollection<ExpenseCategory> ExpenseCategories { get; set; } = new List<ExpenseCategory>();

    }
}
