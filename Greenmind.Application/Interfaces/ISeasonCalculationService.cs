using Greenmind.Application.Dtos.ExportPlans;
using Greenmind.Domain.Entities;

namespace Greenmind.Application.Interfaces
{
    public interface ISeasonCalculationService
    {
        int CalculateCurrentSeasonDay(DateOnly plantingDate, DateOnly? today = null);

        double CalculateCurrentSeasonProgress(DateOnly plantingDate, DateOnly expectedHarvestDate, DateOnly? today = null);

        string DetermineGrowthStage(int currentSeasonDay);

        SeasonCalculationResultDto CalculateSeasonDetails(DateOnly plantingDate, DateOnly expectedHarvestDate, DateOnly? today = null);

        SeasonCalculationResultDto CalculateSeasonDetails(ExportPlan exportPlan, DateOnly? today = null);
    }
}
