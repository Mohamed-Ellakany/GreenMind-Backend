using Microsoft.AspNet.Identity.EntityFramework;

namespace Greenmind.Domain.Entities
{
    public class ApplicationUser : IdentityUser
    {
        public required string FullName { get; set; }

        public bool IsActive { get; set; } = true;

        public Farm? Farm { get; set; }

    }
}
