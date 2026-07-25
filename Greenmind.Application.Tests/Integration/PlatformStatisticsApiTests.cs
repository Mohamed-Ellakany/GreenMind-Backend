using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Greenmind.Application.Contracts.Api;
using Greenmind.Application.Dtos.Admin;
using Greenmind.Domain.Consts;
using Greenmind.Domain.Consts.Enums;
using Greenmind.Domain.Entities;
using Greenmind.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Greenmind.Application.Tests.Integration;

public sealed class PlatformStatisticsApiTests(GreenmindApiFactory factory)
    : IClassFixture<GreenmindApiFactory>
{
    [Fact]
    public async Task AnonymousUser_CannotAccessStatistics()
    {
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/api/admin/statistics");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task FarmOwner_CannotAccessStatistics()
    {
        using var client = CreateClient(RoleNames.FarmOwner, GreenmindApiFactory.FarmOwnerUserId);

        var response = await client.GetAsync("/api/admin/statistics");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Admin_ReceivesFarmAndExportPlanBreakdowns()
    {
        await SeedStatisticsDataAsync();
        using var client = CreateClient(RoleNames.Admin, GreenmindApiFactory.AdminUserId);

        var response = await client.GetAsync("/api/admin/statistics");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var statistics = await ReadDataAsync<PlatformStatisticsDto>(response);
        Assert.Equal(3, statistics.TotalFarms);
        Assert.Equal(2, statistics.ActiveFarms);
        Assert.Equal(1, statistics.InactiveFarms);
        Assert.Equal(3, statistics.TotalExportPlans);
        Assert.Contains(statistics.ExportPlansByCrop, item =>
            item.Name == "Strawberry" && item.ExportPlanCount == 2);
        Assert.Contains(statistics.ExportPlansByCrop, item =>
            item.Name == "Grapes" && item.ExportPlanCount == 1);
        Assert.Contains(statistics.ExportPlansByDestination, item =>
            item.Name == "Germany" && item.ExportPlanCount == 2);
        Assert.Contains(statistics.ExportPlansByDestination, item =>
            item.Name == "Netherlands" && item.ExportPlanCount == 1);
    }

    private async Task SeedStatisticsDataAsync()
    {
        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<GreenmindDbContext>();
        var inactiveUser = new ApplicationUser
        {
            Id = $"inactive-statistics-owner-{Guid.NewGuid():N}",
            UserName = $"inactive.statistics.{Guid.NewGuid():N}",
            FullName = "Inactive Statistics Owner",
            IsActive = false
        };

        dbContext.Users.Add(inactiveUser);
        dbContext.Farms.Add(new Farm
        {
            Name = "Inactive Statistics Farm",
            OwnerUserId = inactiveUser.Id,
            IsApproved = true,
            ApprovedAtUtc = DateTime.UtcNow
        });

        await dbContext.SaveChangesAsync();

        var firstFarm = await dbContext.Farms.SingleAsync(farm =>
            farm.OwnerUserId == GreenmindApiFactory.FarmOwnerUserId);
        var secondFarm = await dbContext.Farms.SingleAsync(farm =>
            farm.OwnerUserId == GreenmindApiFactory.OtherFarmOwnerUserId);
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        dbContext.ExportPlans.AddRange(
            CreateExportPlan(firstFarm.Id, 1, 1, today),
            CreateExportPlan(firstFarm.Id, 1, 1, today),
            CreateExportPlan(secondFarm.Id, 2, 2, today));

        await dbContext.SaveChangesAsync();
    }

    private static ExportPlan CreateExportPlan(
        int farmId,
        int cropId,
        int destinationId,
        DateOnly today) =>
        new()
        {
            FarmId = farmId,
            CropId = cropId,
            DestinationId = destinationId,
            Name = $"Statistics Plan {Guid.NewGuid():N}",
            Governorate = "Giza",
            City = "6th of October",
            AreaInFeddan = 2m,
            Latitude = 29.9,
            Longitude = 30.9,
            PlantingDate = today,
            ExpectedHarvestDate = today.AddDays(90),
            ExpectedExportDate = today.AddDays(95),
            Status = ExportPlanStatus.Planning
        };

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
