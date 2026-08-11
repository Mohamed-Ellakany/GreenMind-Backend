using System.Text.Json;
using Greenmind.Domain.Entities;
using Greenmind.Infrastructure.Iti;
using Greenmind.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Greenmind.Application.Tests.Iti;

public sealed class AiUsageTrackerTests
{
    [Fact]
    public void TryReadUsageResponse_ParsesProviderMetadataWithoutOutputText()
    {
        using var document = JsonDocument.Parse(
            """
            {
              "request_id":"b5766e07-4dd5-4d84-90ab-f9c95576e754",
              "model_id":"deepseek.v3.2",
              "region":"us-east-2",
              "output_text":"private answer that must not be captured",
              "usage":{"input_tokens":23,"output_tokens":799,"total_tokens":822,"stop_reason":"end_turn","budget_state":"ok","fallback_used":false},
              "estimated_cost_usd":"0.001545",
              "actual_cost_usd":"0.001492",
              "status":"active"
            }
            """);

        var parsed = AiUsageTracker.TryReadUsageResponse(document.RootElement, out var usage);

        Assert.True(parsed);
        Assert.Equal("b5766e07-4dd5-4d84-90ab-f9c95576e754", usage.RequestId);
        Assert.Equal("deepseek.v3.2", usage.ModelId);
        Assert.Equal(822, usage.TotalTokens);
        Assert.Equal(0.001492m, usage.ActualCostUsd);
        Assert.DoesNotContain("private answer", JsonSerializer.Serialize(usage));
    }

    [Fact]
    public async Task Tracker_StoresCompletedMetadataOnce_AndBlocksSuspendedUser()
    {
        var services = new ServiceCollection();
        var databaseRoot = new InMemoryDatabaseRoot();
        var databaseName = $"ai-usage-tracker-{Guid.NewGuid():N}";
        services.AddDbContext<GreenmindDbContext>(options =>
            options.UseInMemoryDatabase(databaseName, databaseRoot));
        await using var provider = services.BuildServiceProvider();
        const string userId = "tracked-user";
        int farmId;
        using (var seedScope = provider.CreateScope())
        {
            var db = seedScope.ServiceProvider.GetRequiredService<GreenmindDbContext>();
            db.Users.Add(new ApplicationUser
            {
                Id = userId,
                UserName = "tracked.user",
                FullName = "Tracked User",
                IsActive = true
            });
            var farm = new Farm { Name = "Tracked Farm", OwnerUserId = userId, IsApproved = true };
            db.Farms.Add(farm);
            await db.SaveChangesAsync();
            farmId = farm.Id;
        }

        using var loggerFactory = LoggerFactory.Create(builder =>
            builder.AddConsole().SetMinimumLevel(LogLevel.Debug));
        var tracker = new AiUsageTracker(
            provider.GetRequiredService<IServiceScopeFactory>(),
            loggerFactory.CreateLogger<AiUsageTracker>());
        using var document = JsonDocument.Parse(
            """{"request_id":"unique-request","model_id":"deepseek.v3.2","region":"us-east-2","output_text":"never store this","usage":{"input_tokens":23,"output_tokens":799,"total_tokens":822,"stop_reason":"end_turn","budget_state":"ok","fallback_used":false},"estimated_cost_usd":"0.001545","actual_cost_usd":"0.001492","status":"active"}""");
        Assert.True(AiUsageTracker.TryReadUsageResponse(document.RootElement, out _));
        var context = new ItiAiRequestContext(AiFeatureNames.FarmChat, userId, farmId);

        await tracker.RecordCompletedAsync(document.RootElement, context);
        await tracker.RecordCompletedAsync(document.RootElement, context);

        using (var assertScope = provider.CreateScope())
        {
            var db = assertScope.ServiceProvider.GetRequiredService<GreenmindDbContext>();
            var record = await db.AiUsageRecords.SingleAsync();
            Assert.Equal(userId, record.UserId);
            Assert.Equal(822, record.TotalTokens);
            Assert.Equal(0.001492m, record.ActualCostUsd);
            Assert.Equal(AiFeatureNames.FarmChat, record.Feature);

            var user = await db.Users.SingleAsync(item => item.Id == userId);
            user.IsAiAccessSuspended = true;
            await db.SaveChangesAsync();
        }

        await Assert.ThrowsAsync<AiAccessSuspendedException>(() =>
            tracker.EnsureAccessAllowedAsync(context));
    }
}
