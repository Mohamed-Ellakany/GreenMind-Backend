using System.Globalization;
using System.Text.Json;
using Greenmind.Domain.Entities;
using Greenmind.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Greenmind.Infrastructure.Iti;

public interface IAiUsageTracker
{
    Task EnsureAccessAllowedAsync(
        ItiAiRequestContext? context,
        CancellationToken cancellationToken = default);

    Task RecordCompletedAsync(
        JsonElement response,
        ItiAiRequestContext? context,
        CancellationToken cancellationToken = default);
}

public sealed class AiAccessSuspendedException() : InvalidOperationException(
    "AI access is suspended for this account.");

public sealed class AiUsageLimitExceededException() : InvalidOperationException(
    "AI usage limit exceeded for this account.");

public sealed class AiUsageTracker(
    IServiceScopeFactory scopeFactory,
    ILogger<AiUsageTracker> logger) : IAiUsageTracker
{
    public async Task EnsureAccessAllowedAsync(
        ItiAiRequestContext? context,
        CancellationToken cancellationToken = default)
    {
        if (context is null)
            return;

        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<GreenmindDbContext>();
        var attribution = await ResolveAttributionAsync(db, context, cancellationToken);
        if (attribution.UserId is null)
            return;

        var user = await db.Users
            .AsNoTracking()
            .Where(u => u.Id == attribution.UserId)
            .Select(u => new { u.IsAiAccessSuspended, u.AiUsageLimitUsd })
            .SingleOrDefaultAsync(cancellationToken);
        if (user is null)
            return;

        if (user.IsAiAccessSuspended)
            throw new AiAccessSuspendedException();

        var totalCostUsd = await db.AiUsageRecords
            .AsNoTracking()
            .Where(record => record.UserId == attribution.UserId)
            .SumAsync(record => (decimal?)record.ActualCostUsd, cancellationToken) ?? 0m;

        if (totalCostUsd >= user.AiUsageLimitUsd)
            throw new AiUsageLimitExceededException();
    }

    public async Task RecordCompletedAsync(
        JsonElement response,
        ItiAiRequestContext? context,
        CancellationToken cancellationToken = default)
    {
        if (!TryReadUsageResponse(response, out var usage))
            return;

        try
        {
            using var scope = scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<GreenmindDbContext>();
            var attribution = context is null
                ? new Attribution(null, null, null)
                : await ResolveAttributionAsync(db, context, cancellationToken);
            if (await db.AiUsageRecords
                    .AsNoTracking()
                    .AnyAsync(item => item.ProviderRequestId == usage.RequestId, cancellationToken))
            {
                return;
            }
            db.AiUsageRecords.Add(new AiUsageRecord
            {
                ProviderRequestId = usage.RequestId,
                UserId = attribution.UserId,
                FarmId = attribution.FarmId,
                ExportPlanId = attribution.ExportPlanId,
                Feature = context?.Feature ?? "Unspecified",
                ModelId = usage.ModelId,
                Region = usage.Region,
                InputTokens = usage.InputTokens,
                OutputTokens = usage.OutputTokens,
                TotalTokens = usage.TotalTokens,
                StopReason = usage.StopReason,
                BudgetState = usage.BudgetState,
                FallbackUsed = usage.FallbackUsed,
                EstimatedCostUsd = usage.EstimatedCostUsd,
                ActualCostUsd = usage.ActualCostUsd,
                ProviderStatus = usage.Status,
                CompletedAtUtc = DateTime.UtcNow
            });

            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception)
        {
            // The provider request id is unique. A duplicate means an already captured
            // response was observed again and must not be billed twice.
            logger.LogDebug(exception, "AI usage response {RequestId} was already recorded.", usage.RequestId);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            // Usage telemetry must never discard a valid model response.
            logger.LogWarning(exception, "Completed AI usage response {RequestId} could not be recorded.", usage.RequestId);
        }
    }

    private static async Task<Attribution> ResolveAttributionAsync(
        GreenmindDbContext db,
        ItiAiRequestContext context,
        CancellationToken cancellationToken)
    {
        var userId = context.UserId;
        var farmId = context.FarmId;

        if (context.ExportPlanId is int exportPlanId)
        {
            var plan = await db.ExportPlans
                .AsNoTracking()
                .Where(item => item.Id == exportPlanId)
                .Select(item => new { item.FarmId, item.Farm.OwnerUserId })
                .SingleOrDefaultAsync(cancellationToken);
            if (plan is not null)
            {
                farmId ??= plan.FarmId;
                userId ??= plan.OwnerUserId;
            }
        }

        if (userId is null && farmId is int resolvedFarmId)
        {
            userId = await db.Farms
                .AsNoTracking()
                .Where(item => item.Id == resolvedFarmId)
                .Select(item => item.OwnerUserId)
                .SingleOrDefaultAsync(cancellationToken);
        }

        return new Attribution(userId, farmId, context.ExportPlanId);
    }

    internal static bool TryReadUsageResponse(JsonElement root, out UsageResponse usage)
    {
        if (TryReadUsageObject(root, out usage))
            return true;

        if (root.ValueKind == JsonValueKind.Object)
        {
            foreach (var envelope in new[] { "data", "response", "result" })
            {
                if (root.TryGetProperty(envelope, out var nested) && TryReadUsageObject(nested, out usage))
                    return true;
            }
        }

        usage = default!;
        return false;
    }

    private static bool TryReadUsageObject(JsonElement root, out UsageResponse response)
    {
        response = default!;
        if (root.ValueKind != JsonValueKind.Object ||
            !root.TryGetProperty("request_id", out var requestIdElement) ||
            !root.TryGetProperty("usage", out var usage) ||
            usage.ValueKind != JsonValueKind.Object)
        {
            return false;
        }

        var requestId = ReadString(requestIdElement);
        if (string.IsNullOrWhiteSpace(requestId))
            return false;

        response = new UsageResponse(
            requestId,
            ReadString(root, "model_id") ?? "unknown",
            ReadString(root, "region"),
            ReadInt(usage, "input_tokens"),
            ReadInt(usage, "output_tokens"),
            ReadInt(usage, "total_tokens"),
            ReadString(usage, "stop_reason"),
            ReadString(usage, "budget_state"),
            ReadBool(usage, "fallback_used"),
            ReadDecimal(root, "estimated_cost_usd"),
            ReadDecimal(root, "actual_cost_usd"),
            ReadString(root, "status") ?? "completed");
        return true;
    }

    private static string? ReadString(JsonElement element, string property) =>
        element.TryGetProperty(property, out var value) ? ReadString(value) : null;

    private static string? ReadString(JsonElement element) => element.ValueKind switch
    {
        JsonValueKind.String => element.GetString(),
        JsonValueKind.Number or JsonValueKind.True or JsonValueKind.False => element.GetRawText(),
        _ => null
    };

    private static int ReadInt(JsonElement element, string property) =>
        element.TryGetProperty(property, out var value) &&
        (value.TryGetInt32(out var number) ||
         (value.ValueKind == JsonValueKind.String && int.TryParse(value.GetString(), out number)))
            ? number
            : 0;

    private static bool ReadBool(JsonElement element, string property) =>
        element.TryGetProperty(property, out var value) &&
        (value.ValueKind == JsonValueKind.True ||
         (value.ValueKind == JsonValueKind.String && bool.TryParse(value.GetString(), out var parsed) && parsed));

    private static decimal ReadDecimal(JsonElement element, string property)
    {
        if (!element.TryGetProperty(property, out var value))
            return 0;
        if (value.ValueKind == JsonValueKind.Number && value.TryGetDecimal(out var number))
            return number;
        return value.ValueKind == JsonValueKind.String &&
               decimal.TryParse(value.GetString(), NumberStyles.Number, CultureInfo.InvariantCulture, out number)
            ? number
            : 0;
    }

    private sealed record Attribution(string? UserId, int? FarmId, int? ExportPlanId);

    internal sealed record UsageResponse(
        string RequestId,
        string ModelId,
        string? Region,
        int InputTokens,
        int OutputTokens,
        int TotalTokens,
        string? StopReason,
        string? BudgetState,
        bool FallbackUsed,
        decimal EstimatedCostUsd,
        decimal ActualCostUsd,
        string Status);
}
