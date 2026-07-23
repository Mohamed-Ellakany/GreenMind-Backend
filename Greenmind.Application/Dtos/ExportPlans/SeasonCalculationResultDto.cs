namespace Greenmind.Application.Dtos.ExportPlans
{
    public class SeasonCalculationResultDto
    {
        public int CurrentSeasonDay { get; set; }

        public double CurrentSeasonProgress { get; set; }

        public required string GrowthStage { get; set; }
    }
}
