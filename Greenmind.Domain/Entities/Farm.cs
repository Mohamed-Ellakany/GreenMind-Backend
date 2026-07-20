using System;
using System.Collections.Generic;
using System.Text;

namespace Greenmind.Domain.Entities
{
    public class Farm : BaseEntity
    {
        public string OwnerUserId { get; set; }

        public required string Name { get; set; }

        // Navigation
        public ApplicationUser Owner { get; set; } = null!;

        public ICollection<ExportPlan> ExportPlans { get; set; } = new List<ExportPlan>();
    }
}
