using System;
using System.Collections.Generic;
using System.Text;

namespace Greenmind.Domain.Entities
{
    public class TaskType : BaseEntity
    {
        public required string Name { get; set; }

        public string? Description { get; set; }

        public ICollection<Task> Tasks { get; set; } = new List<Task>();
    }
}
