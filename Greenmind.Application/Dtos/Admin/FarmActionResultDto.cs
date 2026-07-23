namespace Greenmind.Application.Dtos.Admin
{
    public class FarmActionResultDto
    {
        public int FarmId { get; set; }

        public required string Name { get; set; }

        public required string Status { get; set; }

        public bool OwnerIsActive { get; set; }
    }
}
