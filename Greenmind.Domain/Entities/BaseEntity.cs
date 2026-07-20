using System;
using System.Collections.Generic;
using System.Text;

namespace Greenmind.Domain.Entities
{
    public abstract class BaseEntity
    {
        public int Id { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    }
}
