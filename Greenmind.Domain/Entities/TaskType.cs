using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Text;

namespace Greenmind.Domain.Entities
{
    public class TaskType : BaseEntity
    {
        [Required]
        [MaxLength(100)]
        public required string Name { get; set; }

        [MaxLength(500)]
        public string? Description { get; set; }

        public ICollection<FarmTask> FarmTasks { get; set; } = new List<FarmTask>();

    }
}
