using Greenmind.Application.Services;

namespace Greenmind.Application.Tests.Dashboard;

public sealed class SeasonCalculationServiceTests
{
    private readonly SeasonCalculationService _service = new();

    [Fact]
    public void Calculate_BeforePlanting_ReturnsPrePlantingWithZeroProgress()
    {
        var result = _service.Calculate(
            new DateOnly(2026, 8, 10),
            new DateOnly(2026, 9, 8),
            new DateOnly(2026, 8, 9));

        Assert.Equal(0, result.CurrentSeasonDay);
        Assert.Equal(0m, result.ProgressPercentage);
        Assert.Equal("PrePlanting", result.GrowthStage);
    }

    [Fact]
    public void Calculate_AfterHarvest_ClampsProgressAndReturnsHarvestReady()
    {
        var result = _service.Calculate(
            new DateOnly(2026, 8, 10),
            new DateOnly(2026, 9, 8),
            new DateOnly(2026, 9, 20));

        Assert.Equal(30, result.CurrentSeasonDay);
        Assert.Equal(30, result.TotalSeasonDays);
        Assert.Equal(100m, result.ProgressPercentage);
        Assert.Equal("HarvestReady", result.GrowthStage);
    }
}
