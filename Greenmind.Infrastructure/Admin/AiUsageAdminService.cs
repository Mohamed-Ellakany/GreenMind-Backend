using Greenmind.Application.Contracts.Admin;
using Greenmind.Application.Dtos.Admin;
using Greenmind.Domain.Entities;
using Greenmind.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Greenmind.Infrastructure.Admin;

public sealed class AiUsageAdminService(GreenmindDbContext db) : IAiUsageAdminService
{
    private const int MaximumPageSize = 100;

    public async Task<AdminAiUsageOverviewDto> GetOverviewAsync(
        DateTime? fromUtc,
        DateTime? toUtc,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var query = ApplyDateFilter(db.AiUsageRecords.AsNoTracking(), fromUtc, toUtc);
        var aggregates = await BuildAggregatesAsync(query, cancellationToken);
        var users = await query
            .GroupBy(item => new
            {
                item.UserId,
                UserName = item.User != null ? item.User.FullName : "طلبات غير منسوبة",
                Email = item.User != null ? item.User.Email : null,
                FarmName = item.Farm != null ? item.Farm.Name : null
            })
            .Select(group => new AiUsageByUserDto(
                group.Key.UserId,
                group.Key.UserName,
                group.Key.Email,
                group.Key.FarmName,
                group.LongCount(),
                group.Sum(item => (long)item.TotalTokens),
                group.Sum(item => item.ActualCostUsd)))
            .ToListAsync(cancellationToken);
        users = users
            .OrderByDescending(item => item.ActualCostUsd)
            .ThenByDescending(item => item.TotalTokens)
            .ToList();

        return new AdminAiUsageOverviewDto(
            aggregates.Totals,
            aggregates.Daily,
            aggregates.ByModel,
            aggregates.ByFeature,
            users,
            await BuildRequestPageAsync(query, pageNumber, pageSize, cancellationToken));
    }

    public async Task<AdminUserDetailsDto?> GetUserDetailsAsync(
        string userId,
        DateTime? fromUtc,
        DateTime? toUtc,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var user = await db.Users
            .AsNoTracking()
            .Where(item => item.Id == userId)
            .Select(item => new AdminUserProfileDto(
                item.Id,
                item.FullName,
                item.UserName,
                item.Email,
                item.EmailConfirmed,
                item.PhoneNumber,
                item.PhoneNumberConfirmed,
                item.TwoFactorEnabled,
                item.IsActive,
                item.IsAiAccessSuspended,
                item.LockoutEnd.HasValue ? item.LockoutEnd.Value.UtcDateTime : null,
                item.Farm != null ? item.Farm.CreatedAt : null))
            .SingleOrDefaultAsync(cancellationToken);
        if (user is null)
            return null;

        var farms = await db.Farms
            .AsNoTracking()
            .Where(item => item.OwnerUserId == userId)
            .OrderBy(item => item.Name)
            .Select(item => new AdminFarmDetailsDto(
                item.Id,
                item.Name,
                item.IsApproved,
                item.ApprovedAtUtc,
                item.CreatedAt,
                item.ExportPlans.Count))
            .ToListAsync(cancellationToken);

        var plans = await db.ExportPlans
            .AsNoTracking()
            .Where(item => item.Farm.OwnerUserId == userId)
            .OrderByDescending(item => item.CreatedAt)
            .Select(item => new AdminExportPlanDetailsDto(
                item.Id,
                item.FarmId,
                item.Farm.Name,
                item.Name,
                item.Crop.Name,
                item.Destination.Name,
                item.Governorate,
                item.City,
                item.Latitude,
                item.Longitude,
                item.AreaInFeddan,
                item.PlantingDate,
                item.ExpectedHarvestDate,
                item.ExpectedExportDate,
                item.Status.ToString(),
                item.Notes,
                item.PlanningStatus.ToString(),
                item.PlanningRequestedAtUtc,
                item.PlanningStartedAtUtc,
                item.PlanningCompletedAtUtc,
                item.PlanningAttemptCount,
                item.GeneratedTaskCount,
                item.PlanningFailureMessage,
                item.FarmTasks.Count,
                item.DiseaseReports.Count,
                item.CreatedAt))
            .ToListAsync(cancellationToken);

        var usageQuery = ApplyDateFilter(
            db.AiUsageRecords.AsNoTracking().Where(item => item.UserId == userId),
            fromUtc,
            toUtc);
        var aggregates = await BuildAggregatesAsync(usageQuery, cancellationToken);
        var usage = new AdminUserAiUsageDto(
            aggregates.Totals,
            aggregates.Daily,
            aggregates.ByModel,
            aggregates.ByFeature,
            await BuildRequestPageAsync(usageQuery, pageNumber, pageSize, cancellationToken));

        return new AdminUserDetailsDto(user, farms, plans, usage);
    }

    public async Task<bool> SetAiAccessSuspendedAsync(
        string userId,
        bool isSuspended,
        CancellationToken cancellationToken = default)
    {
        var user = await db.Users.SingleOrDefaultAsync(item => item.Id == userId, cancellationToken);
        if (user is null)
            return false;

        user.IsAiAccessSuspended = isSuspended;
        await db.SaveChangesAsync(cancellationToken);
        return true;
    }

    private static IQueryable<AiUsageRecord> ApplyDateFilter(
        IQueryable<AiUsageRecord> query,
        DateTime? fromUtc,
        DateTime? toUtc)
    {
        if (fromUtc.HasValue)
            query = query.Where(item => item.CompletedAtUtc >= fromUtc.Value.Date);
        if (toUtc.HasValue)
            query = query.Where(item => item.CompletedAtUtc < toUtc.Value.Date.AddDays(1));
        return query;
    }

    private static async Task<Aggregates> BuildAggregatesAsync(
        IQueryable<AiUsageRecord> query,
        CancellationToken cancellationToken)
    {
        var totals = await query
            .GroupBy(_ => 1)
            .Select(group => new AiUsageTotalsDto(
                group.LongCount(),
                group.Sum(item => (long)item.InputTokens),
                group.Sum(item => (long)item.OutputTokens),
                group.Sum(item => (long)item.TotalTokens),
                group.Sum(item => item.EstimatedCostUsd),
                group.Sum(item => item.ActualCostUsd),
                group.LongCount(item => item.FallbackUsed)))
            .SingleOrDefaultAsync(cancellationToken)
            ?? EmptyTotals();

        var dailyRows = await query
            .GroupBy(item => item.CompletedAtUtc.Date)
            .Select(group => new
            {
                Date = group.Key,
                Requests = group.LongCount(),
                Tokens = group.Sum(item => (long)item.TotalTokens),
                Cost = group.Sum(item => item.ActualCostUsd)
            })
            .OrderBy(item => item.Date)
            .ToListAsync(cancellationToken);
        var daily = dailyRows.Select(item => new AiUsageDailyDto(
            DateOnly.FromDateTime(item.Date), item.Requests, item.Tokens, item.Cost)).ToList();

        var byModel = await query
            .GroupBy(item => item.ModelId)
            .Select(group => new AiUsageBreakdownDto(
                group.Key,
                group.LongCount(),
                group.Sum(item => (long)item.TotalTokens),
                group.Sum(item => item.ActualCostUsd)))
            .ToListAsync(cancellationToken);
        byModel = byModel
            .OrderByDescending(item => item.ActualCostUsd)
            .ThenByDescending(item => item.TotalTokens)
            .ToList();

        var byFeature = await query
            .GroupBy(item => item.Feature)
            .Select(group => new AiUsageBreakdownDto(
                group.Key,
                group.LongCount(),
                group.Sum(item => (long)item.TotalTokens),
                group.Sum(item => item.ActualCostUsd)))
            .ToListAsync(cancellationToken);
        byFeature = byFeature
            .OrderByDescending(item => item.ActualCostUsd)
            .ThenByDescending(item => item.TotalTokens)
            .ToList();
        return new Aggregates(totals, daily, byModel, byFeature);
    }

    private static async Task<PagedResult<AiUsageRequestDto>> BuildRequestPageAsync(
        IQueryable<AiUsageRecord> query,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken)
    {
        pageNumber = Math.Max(pageNumber, 1);
        pageSize = Math.Clamp(pageSize, 1, MaximumPageSize);
        var totalCount = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderByDescending(item => item.CompletedAtUtc)
            .ThenByDescending(item => item.Id)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .Select(item => new AiUsageRequestDto(
                item.Id,
                item.ProviderRequestId,
                item.UserId,
                item.User != null ? item.User.FullName : null,
                item.Farm != null ? item.Farm.Name : null,
                item.ExportPlanId,
                item.ExportPlan != null ? item.ExportPlan.Name : null,
                item.Feature,
                item.ModelId,
                item.Region,
                item.InputTokens,
                item.OutputTokens,
                item.TotalTokens,
                item.StopReason,
                item.BudgetState,
                item.FallbackUsed,
                item.EstimatedCostUsd,
                item.ActualCostUsd,
                item.ProviderStatus,
                item.CompletedAtUtc))
            .ToListAsync(cancellationToken);
        return new PagedResult<AiUsageRequestDto>(items, pageNumber, pageSize, totalCount);
    }

    private static AiUsageTotalsDto EmptyTotals() => new(0, 0, 0, 0, 0, 0, 0);

    private sealed record Aggregates(
        AiUsageTotalsDto Totals,
        IReadOnlyList<AiUsageDailyDto> Daily,
        IReadOnlyList<AiUsageBreakdownDto> ByModel,
        IReadOnlyList<AiUsageBreakdownDto> ByFeature);
}
