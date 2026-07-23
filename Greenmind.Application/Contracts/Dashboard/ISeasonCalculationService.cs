using Greenmind.Application.Dtos.Dashboard;

namespace Greenmind.Application.Contracts.Dashboard;

public interface ISeasonCalculationService
{
    SeasonProgressDto Calculate(
        DateOnly plantingDate,
        DateOnly expectedHarvestDate,
        DateOnly asOfDate);
}
