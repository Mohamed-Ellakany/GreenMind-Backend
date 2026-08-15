using Microsoft.AspNetCore.Identity;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Greenmind.Domain.Entities
{
    public class ApplicationUser : IdentityUser
    {
        [Required]
        [MaxLength(150)]
        public required string FullName { get; set; }

        public bool IsActive { get; set; } = true;

        public bool IsAiAccessSuspended { get; set; }

        [Column(TypeName = "decimal(18,8)")]
        public decimal AiUsageLimitUsd { get; set; } = 0.10m;

        public Farm? Farm { get; set; }

        public ICollection<AdminInvitation> CreatedAdminInvitations { get; set; } = new List<AdminInvitation>();

        public ICollection<AiUsageRecord> AiUsageRecords { get; set; } = new List<AiUsageRecord>();

        public ICollection<AiLimitRequest> AiLimitRequests { get; set; } = new List<AiLimitRequest>();

    }
}
