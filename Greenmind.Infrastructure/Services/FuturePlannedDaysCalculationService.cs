using Greenmind.Application.Services;
using Greenmind.Domain.Entities;
using Greenmind.Application.Contracts.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Greenmind.Infrastructure.Services;

public class FuturePlannedDaysCalculationService : IFuturePlannedDaysCalculationService
{
    private readonly IUnitOfWork _uow;

    public FuturePlannedDaysCalculationService(IUnitOfWork uow)
    {
        _uow = uow;
    }

    public async Task<List<DateOnly>> GetMissingFuturePlannedDatesAsync(ExportPlan exportPlan, CancellationToken cancellationToken = default)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var start = today.AddDays(1);
        var end = today.AddDays(7);

        var existingDates = await _uow.Tasks.Query()
            .Where(t => t.ExportPlanId == exportPlan.Id && t.PlannedDate >= start && t.PlannedDate <= end)
            .AsNoTracking()
            .Select(t => t.PlannedDate)
            .Distinct()
            .ToListAsync(cancellationToken);

        var missing = new List<DateOnly>();
        for (var d = start; d <= end; d = d.AddDays(1))
        {
            if (!existingDates.Contains(d)) missing.Add(d);
        }

        return missing;
    }

    public async Task<int> CountFuturePlannedDaysAsync(ExportPlan exportPlan, int days = 7, CancellationToken cancellationToken = default)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var start = today.AddDays(1);
        var end = today.AddDays(days);

        var count = await _uow.Tasks.Query()
            .Where(t => t.ExportPlanId == exportPlan.Id && t.PlannedDate >= start && t.PlannedDate <= end)
            .AsNoTracking()
            .Select(t => t.PlannedDate)
            .Distinct()
            .CountAsync(cancellationToken);

        return count;
    }
}
