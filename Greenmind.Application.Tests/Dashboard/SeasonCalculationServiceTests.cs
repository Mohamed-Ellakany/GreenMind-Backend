using Greenmind.Application.Services;
using Greenmind.Domain.Consts;
using Xunit;

namespace Greenmind.Application.Tests.Dashboard;

public sealed class SeasonCalculationServiceTests
{
    private readonly SeasonCalculationService _service = new();

    [Fact]
    public void Calculate_BeforePlanting_ReturnsPrePlantingWithZeroProgress()
    {
        var plantingDate = new DateOnly(2026, 8, 10);
        var harvestDate = new DateOnly(2026, 9, 8);
        var asOfDate = new DateOnly(2026, 8, 9);

        var result = _service.Calculate(plantingDate, harvestDate, asOfDate);

        Assert.Equal(0, result.CurrentSeasonDay);
        Assert.Equal(0m, result.ProgressPercentage);
        Assert.Equal(StrawberryGrowthStageConstants.Stages.PrePlanting, result.GrowthStage);
    }

    [Fact]
    public void Calculate_OnPlantingDate_ReturnsDayOne()
    {
        var plantingDate = new DateOnly(2026, 8, 10);
        var harvestDate = new DateOnly(2026, 9, 8); // 30 days total
        var asOfDate = new DateOnly(2026, 8, 10);

        var result = _service.Calculate(plantingDate, harvestDate, asOfDate);

        Assert.Equal(1, result.CurrentSeasonDay);
        Assert.Equal(30, result.TotalSeasonDays);
        Assert.Equal(3.33m, result.ProgressPercentage);
        Assert.Equal(StrawberryGrowthStageConstants.Stages.Establishment, result.GrowthStage);
    }

    [Fact]
    public void Calculate_SecondDay_ReturnsDayTwo()
    {
        var plantingDate = new DateOnly(2026, 8, 10);
        var harvestDate = new DateOnly(2026, 9, 8);
        var asOfDate = new DateOnly(2026, 8, 11);

        var result = _service.Calculate(plantingDate, harvestDate, asOfDate);

        Assert.Equal(2, result.CurrentSeasonDay);
    }

    [Fact]
    public void Calculate_MiddleOfSeason_ReturnsExactProgressAndStage()
    {
        var plantingDate = new DateOnly(2026, 1, 1);
        var harvestDate = new DateOnly(2026, 4, 10); // 100 days total
        var asOfDate = new DateOnly(2026, 2, 19); // 50 days elapsed (Day 50)

        var result = _service.Calculate(plantingDate, harvestDate, asOfDate);

        Assert.Equal(50, result.CurrentSeasonDay);
        Assert.Equal(100, result.TotalSeasonDays);
        Assert.Equal(50.00m, result.ProgressPercentage);
        Assert.Equal(StrawberryGrowthStageConstants.Stages.Flowering, result.GrowthStage);
    }

    [Fact]
    public void Calculate_OnHarvestDay_ReturnsHundredPercentAndHarvestReady()
    {
        var plantingDate = new DateOnly(2026, 8, 10);
        var harvestDate = new DateOnly(2026, 9, 8);
        var asOfDate = new DateOnly(2026, 9, 8);

        var result = _service.Calculate(plantingDate, harvestDate, asOfDate);

        Assert.Equal(30, result.CurrentSeasonDay);
        Assert.Equal(30, result.TotalSeasonDays);
        Assert.Equal(100m, result.ProgressPercentage);
        Assert.Equal(StrawberryGrowthStageConstants.Stages.HarvestReady, result.GrowthStage);
    }

    [Fact]
    public void Calculate_AfterHarvest_ClampsProgressAndReturnsHarvestReady()
    {
        var plantingDate = new DateOnly(2026, 8, 10);
        var harvestDate = new DateOnly(2026, 9, 8);
        var asOfDate = new DateOnly(2026, 9, 20);

        var result = _service.Calculate(plantingDate, harvestDate, asOfDate);

        Assert.Equal(30, result.CurrentSeasonDay);
        Assert.Equal(30, result.TotalSeasonDays);
        Assert.Equal(100m, result.ProgressPercentage);
        Assert.Equal(StrawberryGrowthStageConstants.Stages.HarvestReady, result.GrowthStage);
    }

    [Theory]
    [InlineData(1, "Establishment")]   // ~3.33%
    [InlineData(8, "Vegetative")]      // ~26.67%
    [InlineData(16, "Flowering")]      // ~53.33%
    [InlineData(24, "Fruiting")]       // ~80.00%
    public void Calculate_GrowthStageTransitions_ReturnsCorrectStage(int dayOffset, string expectedStage)
    {
        var plantingDate = new DateOnly(2026, 8, 1);
        var harvestDate = new DateOnly(2026, 8, 30); // 30 days total
        var asOfDate = plantingDate.AddDays(dayOffset - 1);

        var result = _service.Calculate(plantingDate, harvestDate, asOfDate);

        Assert.Equal(expectedStage, result.GrowthStage);
    }
}
