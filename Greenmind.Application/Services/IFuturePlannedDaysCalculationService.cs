using Greenmind.Domain.Entities;

namespace Greenmind.Application.Services;

public interface IFuturePlannedDaysCalculationService
{
    /// <summary>
    /// Returns the list of missing future planned dates (from tomorrow..tomorrow+6) for the given export plan.
    /// </summary>
    Task<List<DateOnly>> GetMissingFuturePlannedDatesAsync(ExportPlan exportPlan, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the count of distinct future planned dates within the next N days (default 7).
    /// </summary>
    Task<int> CountFuturePlannedDaysAsync(ExportPlan exportPlan, int days = 7, CancellationToken cancellationToken = default);
}
