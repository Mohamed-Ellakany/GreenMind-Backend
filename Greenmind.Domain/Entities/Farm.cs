using Greenmind.Domain.Consts.Enums;

namespace Greenmind.Domain.Entities
{
    public class Farm : BaseEntity
    {
        public int OwnerUserId { get; set; }

        public required string Name { get; set; }

        public FarmStatus Status { get; set; } = FarmStatus.Pending;

        // Navigation
        public ApplicationUser Owner { get; set; } = null!;

        public ICollection<ExportPlan> ExportPlans { get; set; } = new List<ExportPlan>();
    }
}
