
using Microsoft.AspNetCore.Identity;

namespace Greenmind.Domain.Entities
{
    public class ApplicationUser : IdentityUser<int>
    {
        public required string FullName { get; set; }

        public bool IsActive { get; set; }

        public Farm? Farm { get; set; }
    }
}
