using Greenmind.Application.Contracts.Admin;
using Greenmind.Application.Dtos.Admin;
using Greenmind.Domain.Consts.Enums;
using Greenmind.Domain.Entities;
using Greenmind.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Greenmind.Infrastructure.Admin;

public sealed class AiLimitRequestService(GreenmindDbContext db) : IAiLimitRequestService
{
    public async Task<UserBudgetStatusDto> GetUserBudgetStatusAsync(
        string userId,
        CancellationToken cancellationToken = default)
    {
        var limitUsd = await db.Users
            .AsNoTracking()
            .Where(user => user.Id == userId)
            .Select(user => user.AiUsageLimitUsd)
            .SingleOrDefaultAsync(cancellationToken);

        var usedUsd = await db.AiUsageRecords
            .AsNoTracking()
            .Where(record => record.UserId == userId)
            .SumAsync(record => (decimal?)record.ActualCostUsd, cancellationToken) ?? 0m;

        var hasPending = await db.AiLimitRequests
            .AsNoTracking()
            .AnyAsync(
                request => request.UserId == userId && request.Status == AiLimitRequestStatus.Pending,
                cancellationToken);

        return new UserBudgetStatusDto(limitUsd, usedUsd, usedUsd >= limitUsd, hasPending);
    }

    public async Task<bool> SubmitRequestAsync(
        string userId,
        string contactName,
        string contactPhone,
        CancellationToken cancellationToken = default)
    {
        var alreadyPending = await db.AiLimitRequests
            .AsNoTracking()
            .AnyAsync(
                request => request.UserId == userId && request.Status == AiLimitRequestStatus.Pending,
                cancellationToken);
        if (alreadyPending)
            return false;

        db.AiLimitRequests.Add(new AiLimitRequest
        {
            UserId = userId,
            ContactName = contactName.Trim(),
            ContactPhone = contactPhone.Trim(),
            RequestedAtUtc = DateTime.UtcNow,
            Status = AiLimitRequestStatus.Pending
        });

        await db.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<IReadOnlyList<AiLimitRequestDto>> GetRequestsAsync(
        bool pendingOnly = true,
        CancellationToken cancellationToken = default)
    {
        var query = db.AiLimitRequests
            .AsNoTracking()
            .Include(request => request.User)
            .AsQueryable();

        if (pendingOnly)
            query = query.Where(request => request.Status == AiLimitRequestStatus.Pending);

        var requests = await query
            .OrderByDescending(request => request.RequestedAtUtc)
            .ToListAsync(cancellationToken);

        var userIds = requests
            .Select(request => request.UserId)
            .Distinct()
            .ToList();

        var usageLookup = await db.AiUsageRecords
            .AsNoTracking()
            .Where(record => userIds.Contains(record.UserId!))
            .GroupBy(record => record.UserId!)
            .Select(group => new { UserId = group.Key, TotalCost = group.Sum(item => item.ActualCostUsd) })
            .ToDictionaryAsync(item => item.UserId, item => item.TotalCost, cancellationToken);

        return requests.Select(request => new AiLimitRequestDto(
            request.Id,
            request.UserId,
            request.User?.FullName ?? "—",
            request.User?.Email,
            request.ContactName,
            request.ContactPhone,
            request.User?.AiUsageLimitUsd ?? 0.10m,
            usageLookup.GetValueOrDefault(request.UserId, 0m),
            request.Status,
            request.AdminNote,
            request.NewLimitUsd,
            request.RequestedAtUtc,
            request.ResolvedAtUtc)).ToList();
    }

    public async Task<bool> ResolveRequestAsync(
        int requestId,
        string adminUserId,
        bool approved,
        decimal? newLimitUsd,
        string? adminNote,
        CancellationToken cancellationToken = default)
    {
        var request = await db.AiLimitRequests
            .SingleOrDefaultAsync(item => item.Id == requestId, cancellationToken);
        if (request is null || request.Status != AiLimitRequestStatus.Pending)
            return false;

        request.Status = approved ? AiLimitRequestStatus.Approved : AiLimitRequestStatus.Rejected;
        request.AdminNote = adminNote?.Trim();
        request.ResolvedAtUtc = DateTime.UtcNow;
        request.ResolvedByUserId = adminUserId;

        if (approved && newLimitUsd.HasValue && newLimitUsd.Value > 0)
        {
            var user = await db.Users
                .SingleOrDefaultAsync(user => user.Id == request.UserId, cancellationToken);
            if (user is not null)
            {
                user.AiUsageLimitUsd += newLimitUsd.Value;
                request.NewLimitUsd = user.AiUsageLimitUsd;
            }
            else
            {
                request.NewLimitUsd = newLimitUsd.Value;
            }
        }

        await db.SaveChangesAsync(cancellationToken);
        return true;
    }
}
