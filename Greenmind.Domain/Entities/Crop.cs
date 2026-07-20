using System;
using System.Collections.Generic;
using System.Text;

namespace Greenmind.Domain.Entities
{
    public class Crop : BaseEntity
    {
        public required string Name { get; set; }

        public string? Description { get; set; }

        public ICollection<ExportPlan> ExportPlans { get; set; } = new List<ExportPlan>();
    }
}
