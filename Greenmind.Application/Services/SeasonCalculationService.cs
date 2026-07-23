using Greenmind.Application.Dtos.ExportPlans;
using Greenmind.Application.Interfaces;
using Greenmind.Domain.Consts;
using Greenmind.Domain.Entities;

namespace Greenmind.Application.Services
{
    public class SeasonCalculationService : ISeasonCalculationService
    {
        public int CalculateCurrentSeasonDay(DateOnly plantingDate, DateOnly? today = null)
        {
            var currentDate = today ?? DateOnly.FromDateTime(DateTime.UtcNow);

            if (currentDate < plantingDate)
            {
                return 0;
            }

            return currentDate.DayNumber - plantingDate.DayNumber + 1;
        }

        public double CalculateCurrentSeasonProgress(DateOnly plantingDate, DateOnly expectedHarvestDate, DateOnly? today = null)
        {
            var currentDate = today ?? DateOnly.FromDateTime(DateTime.UtcNow);

            if (currentDate < plantingDate)
            {
                return 0.0;
            }

            if (currentDate >= expectedHarvestDate)
            {
                return 100.0;
            }

            int totalDays = expectedHarvestDate.DayNumber - plantingDate.DayNumber;

            if (totalDays <= 0)
            {
                return 100.0;
            }

            int elapsedDays = currentDate.DayNumber - plantingDate.DayNumber;
            double progress = ((double)elapsedDays / totalDays) * 100.0;

            double clampedProgress = Math.Clamp(progress, 0.0, 100.0);
            return Math.Round(clampedProgress, 2);
        }

        public string DetermineGrowthStage(int currentSeasonDay)
        {
            if (currentSeasonDay <= 0)
            {
                return StrawberryGrowthStageConstants.Stages.NotStarted;
            }

            foreach (var (minDay, maxDay, stageName) in StrawberryGrowthStageConstants.DefaultStageRanges)
            {
                if (currentSeasonDay >= minDay && currentSeasonDay <= maxDay)
                {
                    return stageName;
                }
            }

            return StrawberryGrowthStageConstants.Stages.Harvesting;
        }

        public SeasonCalculationResultDto CalculateSeasonDetails(DateOnly plantingDate, DateOnly expectedHarvestDate, DateOnly? today = null)
        {
            int seasonDay = CalculateCurrentSeasonDay(plantingDate, today);
            double progress = CalculateCurrentSeasonProgress(plantingDate, expectedHarvestDate, today);
            string growthStage = DetermineGrowthStage(seasonDay);

            return new SeasonCalculationResultDto
            {
                CurrentSeasonDay = seasonDay,
                CurrentSeasonProgress = progress,
                GrowthStage = growthStage
            };
        }

        public SeasonCalculationResultDto CalculateSeasonDetails(ExportPlan exportPlan, DateOnly? today = null)
        {
            ArgumentNullException.ThrowIfNull(exportPlan);
            return CalculateSeasonDetails(exportPlan.PlantingDate, exportPlan.ExpectedHarvestDate, today);
        }
    }
}
