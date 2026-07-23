using Greenmind.Application.Services;
using Greenmind.Domain.Consts;
using Xunit;

namespace Greenmind.Application.UnitTests
{
    public class SeasonCalculationServiceTests
    {
        private readonly SeasonCalculationService _service = new();

        [Fact]
        public void CalculateCurrentSeasonDay_BeforePlantingDate_ReturnsZero()
        {
            var plantingDate = new DateOnly(2026, 3, 1);
            var today = new DateOnly(2026, 2, 28);

            int result = _service.CalculateCurrentSeasonDay(plantingDate, today);

            Assert.Equal(0, result);
        }

        [Fact]
        public void CalculateCurrentSeasonDay_OnPlantingDate_ReturnsOne()
        {
            var plantingDate = new DateOnly(2026, 3, 1);
            var today = new DateOnly(2026, 3, 1);

            int result = _service.CalculateCurrentSeasonDay(plantingDate, today);

            Assert.Equal(1, result);
        }

        [Fact]
        public void CalculateCurrentSeasonDay_SecondDay_ReturnsTwo()
        {
            var plantingDate = new DateOnly(2026, 3, 1);
            var today = new DateOnly(2026, 3, 2);

            int result = _service.CalculateCurrentSeasonDay(plantingDate, today);

            Assert.Equal(2, result);
        }

        [Fact]
        public void CalculateCurrentSeasonDay_MiddleOfSeason_ReturnsExactDayIndex()
        {
            var plantingDate = new DateOnly(2026, 3, 1);
            var today = new DateOnly(2026, 4, 19); // 49 days elapsed + 1 = 50th day

            int result = _service.CalculateCurrentSeasonDay(plantingDate, today);

            Assert.Equal(50, result);
        }

        [Fact]
        public void CalculateCurrentSeasonDay_OnHarvestDay_ReturnsExactHarvestDayIndex()
        {
            var plantingDate = new DateOnly(2026, 3, 1);
            var today = new DateOnly(2026, 6, 8); // 99 days elapsed + 1 = 100th day

            int result = _service.CalculateCurrentSeasonDay(plantingDate, today);

            Assert.Equal(100, result);
        }

        [Fact]
        public void CalculateCurrentSeasonProgress_BeforePlanting_ReturnsZero()
        {
            var plantingDate = new DateOnly(2026, 3, 1);
            var harvestDate = new DateOnly(2026, 6, 8);
            var today = new DateOnly(2026, 2, 20);

            double progress = _service.CalculateCurrentSeasonProgress(plantingDate, harvestDate, today);

            Assert.Equal(0.0, progress);
        }

        [Fact]
        public void CalculateCurrentSeasonProgress_BeginningOfSeason_ReturnsZeroOrStartPercentage()
        {
            var plantingDate = new DateOnly(2026, 3, 1);
            var harvestDate = new DateOnly(2026, 6, 8); // 99 days total
            var today = new DateOnly(2026, 3, 1); // Day 0 elapsed

            double progress = _service.CalculateCurrentSeasonProgress(plantingDate, harvestDate, today);

            Assert.Equal(0.0, progress);
        }

        [Fact]
        public void CalculateCurrentSeasonProgress_MiddleOfSeason_ReturnsExactPercentage()
        {
            var plantingDate = new DateOnly(2026, 1, 1);
            var harvestDate = new DateOnly(2026, 4, 11); // 100 days duration
            var today = new DateOnly(2026, 2, 20); // 50 days elapsed

            double progress = _service.CalculateCurrentSeasonProgress(plantingDate, harvestDate, today);

            Assert.Equal(50.0, progress);
        }

        [Fact]
        public void CalculateCurrentSeasonProgress_OnHarvestDay_ReturnsHundred()
        {
            var plantingDate = new DateOnly(2026, 3, 1);
            var harvestDate = new DateOnly(2026, 6, 8);
            var today = new DateOnly(2026, 6, 8);

            double progress = _service.CalculateCurrentSeasonProgress(plantingDate, harvestDate, today);

            Assert.Equal(100.0, progress);
        }

        [Fact]
        public void CalculateCurrentSeasonProgress_AfterHarvest_ReturnsHundred()
        {
            var plantingDate = new DateOnly(2026, 3, 1);
            var harvestDate = new DateOnly(2026, 6, 8);
            var today = new DateOnly(2026, 6, 20); // 12 days past harvest

            double progress = _service.CalculateCurrentSeasonProgress(plantingDate, harvestDate, today);

            Assert.Equal(100.0, progress);
        }

        [Theory]
        [InlineData(-10, StrawberryGrowthStageConstants.Stages.NotStarted)]
        [InlineData(0, StrawberryGrowthStageConstants.Stages.NotStarted)]
        [InlineData(1, StrawberryGrowthStageConstants.Stages.Establishment)]
        [InlineData(30, StrawberryGrowthStageConstants.Stages.Establishment)]
        [InlineData(31, StrawberryGrowthStageConstants.Stages.Vegetative)]
        [InlineData(60, StrawberryGrowthStageConstants.Stages.Vegetative)]
        [InlineData(61, StrawberryGrowthStageConstants.Stages.Flowering)]
        [InlineData(90, StrawberryGrowthStageConstants.Stages.Flowering)]
        [InlineData(91, StrawberryGrowthStageConstants.Stages.FruitDevelopment)]
        [InlineData(120, StrawberryGrowthStageConstants.Stages.FruitDevelopment)]
        [InlineData(121, StrawberryGrowthStageConstants.Stages.Harvesting)]
        [InlineData(200, StrawberryGrowthStageConstants.Stages.Harvesting)]
        public void GrowthStage_BoundaryTransitions_ReturnsCorrectStage(int day, string expectedStage)
        {
            string stage = _service.DetermineGrowthStage(day);
            Assert.Equal(expectedStage, stage);
        }
    }
}
