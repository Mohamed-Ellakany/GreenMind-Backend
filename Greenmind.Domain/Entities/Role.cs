namespace Greenmind.Domain.Entities
{
    public class Role : BaseEntity
    {
        public required string Name { get; set; }

        public ICollection<ApplicationUser> Users { get; set; } = new List<ApplicationUser>();
    }
}
