namespace Greenmind.Application.Dtos.Admin
{
    public class PendingFarmDto
    {
        public int Id { get; set; }

        public required string Name { get; set; }

        public required string OwnerFullName { get; set; }

        public required string OwnerEmail { get; set; }

        public DateTime CreatedAt { get; set; }
    }
}
