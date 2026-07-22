using Greenmind.Application.Contracts.Dashboard;
using Greenmind.Application.Dtos.Dashboard;

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
        var elapsedDays = asOfDate.DayNumber - plantingDate.DayNumber + 1;
        var currentSeasonDay = Math.Clamp(elapsedDays, 0, totalSeasonDays);
        var progressPercentage = Math.Round(
            currentSeasonDay * 100m / totalSeasonDays,
            2,
            MidpointRounding.AwayFromZero);

        return new SeasonProgressDto(
            currentSeasonDay,
            totalSeasonDays,
            progressPercentage,
            GetGrowthStage(currentSeasonDay, totalSeasonDays));
    }

    private static string GetGrowthStage(int currentSeasonDay, int totalSeasonDays)
    {
        if (currentSeasonDay == 0)
        {
            return "PrePlanting";
        }

        if (currentSeasonDay == totalSeasonDays)
        {
            return "HarvestReady";
        }

        var progress = currentSeasonDay * 100m / totalSeasonDays;

        return progress switch
        {
            < 25m => "Establishment",
            < 50m => "Vegetative",
            < 75m => "Flowering",
            _ => "Fruiting"
        };
    }
}
