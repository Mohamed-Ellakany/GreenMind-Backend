

using Microsoft.AspNetCore.Identity;
using System.ComponentModel.DataAnnotations;

namespace Greenmind.Domain.Entities
{
    public class ApplicationUser : IdentityUser
    {
        [Required]
        [MaxLength(150)]
        public required string FullName { get; set; }

        public bool IsActive { get; set; } = true;

        public Farm? Farm { get; set; }

        public ICollection<AdminInvitation> CreatedAdminInvitations { get; set; } = new List<AdminInvitation>();

    }
}
