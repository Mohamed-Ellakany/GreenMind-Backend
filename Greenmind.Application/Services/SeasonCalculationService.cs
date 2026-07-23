using Greenmind.Application.Contracts.Dashboard;
using Greenmind.Application.Dtos.Dashboard;
using Greenmind.Domain.Consts;

namespace Greenmind.Application.Services;

public sealed class SeasonCalculationService : ISeasonCalculationService
{
    public SeasonProgressDto Calculate(
        DateOnly plantingDate,
        DateOnly expectedHarvestDate,
        DateOnly asOfDate)
    {
        var totalSeasonDays = Math.Max(
            1,
            expectedHarvestDate.DayNumber - plantingDate.DayNumber + 1);

        int currentSeasonDay;
        decimal progressPercentage;

        if (asOfDate < plantingDate)
        {
            currentSeasonDay = 0;
            progressPercentage = 0m;
        }
        else if (asOfDate >= expectedHarvestDate)
        {
            currentSeasonDay = totalSeasonDays;
            progressPercentage = 100m;
        }
        else
        {
            currentSeasonDay = asOfDate.DayNumber - plantingDate.DayNumber + 1;
            var unroundedProgress = (decimal)currentSeasonDay * 100m / totalSeasonDays;
            progressPercentage = Math.Round(
                Math.Clamp(unroundedProgress, 0m, 100m),
                2,
                MidpointRounding.AwayFromZero);
        }

        var growthStage = GetGrowthStage(currentSeasonDay, totalSeasonDays, progressPercentage, asOfDate < plantingDate, asOfDate >= expectedHarvestDate);

        return new SeasonProgressDto(
            currentSeasonDay,
            totalSeasonDays,
            progressPercentage,
            growthStage);
    }

    private static string GetGrowthStage(
        int currentSeasonDay,
        int totalSeasonDays,
        decimal progressPercentage,
        bool isBeforePlanting,
        bool isAfterHarvest)
    {
        if (isBeforePlanting || currentSeasonDay == 0)
        {
            return StrawberryGrowthStageConstants.Stages.PrePlanting;
        }

        if (isAfterHarvest)
        {
            return StrawberryGrowthStageConstants.Stages.HarvestReady;
        }

        foreach (var (maxProgress, stageName) in StrawberryGrowthStageConstants.DefaultProgressStageRanges)
        {
            if (progressPercentage < maxProgress)
            {
                return stageName;
            }
        }

        return StrawberryGrowthStageConstants.Stages.Fruiting;
    }
}
