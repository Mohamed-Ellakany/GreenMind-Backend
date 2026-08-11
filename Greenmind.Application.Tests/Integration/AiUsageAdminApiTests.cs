using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Greenmind.Application.Contracts.Api;
using Greenmind.Application.Dtos.Admin;
using Greenmind.Domain.Consts;
using Greenmind.Domain.Entities;
using Greenmind.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Greenmind.Application.Tests.Integration;

public sealed class AiUsageAdminApiTests(GreenmindApiFactory factory)
    : IClassFixture<GreenmindApiFactory>
{
    [Fact]
    public async Task Admin_ReceivesPlatformAndPerUserUsageWithoutOutputText()
    {
        await SeedUsageAsync();
        using var client = CreateClient(RoleNames.Admin, GreenmindApiFactory.AdminUserId);

        var overviewResponse = await client.GetAsync("/api/admin/ai-usage?pageSize=20");
        Assert.True(
            overviewResponse.StatusCode == HttpStatusCode.OK,
            await overviewResponse.Content.ReadAsStringAsync());
        var overview = await ReadDataAsync<AdminAiUsageOverviewDto>(overviewResponse);
        Assert.Equal(2, overview.Totals.RequestCount);
        Assert.Equal(1_200, overview.Totals.TotalTokens);
        Assert.Equal(0.0032m, overview.Totals.ActualCostUsd);
        Assert.Contains(overview.UsageByFeature, item =>
            item.Key == "FarmChat" && item.RequestCount == 1);
        Assert.Equal(2, overview.Requests.TotalCount);

        var userResponse = await client.GetAsync(
            $"/api/admin/users/{GreenmindApiFactory.FarmOwnerUserId}?pageSize=20");
        Assert.Equal(HttpStatusCode.OK, userResponse.StatusCode);
        var details = await ReadDataAsync<AdminUserDetailsDto>(userResponse);
        Assert.Equal(GreenmindApiFactory.FarmOwnerUserId, details.User.Id);
        Assert.Single(details.Farms);
        Assert.Equal(1, details.AiUsage.Totals.RequestCount);
        Assert.Equal("usage-request-owner", details.AiUsage.Requests.Items[0].ProviderRequestId);
    }

    [Fact]
    public async Task Admin_CanSuspendAndRestoreUserAiAccess()
    {
        using var client = CreateClient(RoleNames.Admin, GreenmindApiFactory.AdminUserId);

        var suspend = await client.PatchAsJsonAsync(
            $"/api/admin/users/{GreenmindApiFactory.FarmOwnerUserId}/ai-access",
            new { isSuspended = true });
        Assert.Equal(HttpStatusCode.NoContent, suspend.StatusCode);
        await AssertSuspendedAsync(true);

        var restore = await client.PatchAsJsonAsync(
            $"/api/admin/users/{GreenmindApiFactory.FarmOwnerUserId}/ai-access",
            new { isSuspended = false });
        Assert.Equal(HttpStatusCode.NoContent, restore.StatusCode);
        await AssertSuspendedAsync(false);
    }

    [Fact]
    public async Task FarmOwner_CannotReadAdminUsage()
    {
        using var client = CreateClient(RoleNames.FarmOwner, GreenmindApiFactory.FarmOwnerUserId);
        var response = await client.GetAsync("/api/admin/ai-usage");
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    private async Task SeedUsageAsync()
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<GreenmindDbContext>();
        if (await db.AiUsageRecords.AnyAsync())
            return;

        var ownerFarm = await db.Farms.SingleAsync(item =>
            item.OwnerUserId == GreenmindApiFactory.FarmOwnerUserId);
        var otherFarm = await db.Farms.SingleAsync(item =>
            item.OwnerUserId == GreenmindApiFactory.OtherFarmOwnerUserId);
        db.AiUsageRecords.AddRange(
            CreateUsage("usage-request-owner", GreenmindApiFactory.FarmOwnerUserId, ownerFarm.Id, "FarmChat", 700, 0.002m),
            CreateUsage("usage-request-other", GreenmindApiFactory.OtherFarmOwnerUserId, otherFarm.Id, "DiseaseImageAnalysis", 500, 0.0012m));
        await db.SaveChangesAsync();
    }

    private static AiUsageRecord CreateUsage(
        string requestId,
        string userId,
        int farmId,
        string feature,
        int tokens,
        decimal cost) => new()
    {
        ProviderRequestId = requestId,
        UserId = userId,
        FarmId = farmId,
        Feature = feature,
        ModelId = "deepseek.v3.2",
        Region = "us-east-2",
        InputTokens = 100,
        OutputTokens = tokens - 100,
        TotalTokens = tokens,
        StopReason = "end_turn",
        BudgetState = "ok",
        EstimatedCostUsd = cost + 0.0001m,
        ActualCostUsd = cost,
        ProviderStatus = "active",
        CompletedAtUtc = DateTime.UtcNow
    };

    private async Task AssertSuspendedAsync(bool expected)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<GreenmindDbContext>();
        var actual = await db.Users
            .Where(item => item.Id == GreenmindApiFactory.FarmOwnerUserId)
            .Select(item => item.IsAiAccessSuspended)
            .SingleAsync();
        Assert.Equal(expected, actual);
    }

    private HttpClient CreateClient(string role, string userId)
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Test-Role", role);
        client.DefaultRequestHeaders.Add("X-Test-UserId", userId);
        return client;
    }

    private static async Task<T> ReadDataAsync<T>(HttpResponseMessage response)
    {
        var envelope = await response.Content.ReadFromJsonAsync<ApiResponse<JsonElement>>();
        Assert.NotNull(envelope);
        Assert.True(envelope.Succeeded);
        return envelope.Data.Deserialize<T>(new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
    }
}
