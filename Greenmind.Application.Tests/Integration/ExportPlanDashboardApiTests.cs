using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Greenmind.Application.Contracts.Api;
using Greenmind.Application.Dtos.Dashboard;
using Greenmind.Domain.Consts;
using Greenmind.Domain.Consts.Enums;
using Greenmind.Domain.Entities;
using Greenmind.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using DomainTaskStatus = Greenmind.Domain.Consts.Enums.TaskStatus;

namespace Greenmind.Application.Tests.Integration;

public sealed class ExportPlanDashboardApiTests(GreenmindApiFactory factory)
    : IClassFixture<GreenmindApiFactory>
{
    [Fact]
    public async Task Owner_CanGetDashboardForOwnExportPlan()
    {
        var exportPlanId = await CreateExportPlanWithDashboardDataAsync();

        using var client = CreateFarmOwnerClient(GreenmindApiFactory.FarmOwnerUserId);
        var response = await client.GetAsync($"/api/farm-owner/export-plans/{exportPlanId}/dashboard");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var dashboard = await ReadDataAsync<ExportPlanDashboardDto>(response);
        Assert.Equal(exportPlanId, dashboard.ExportPlan.Id);
        Assert.Equal("Strawberry", dashboard.ExportPlan.Crop);
        Assert.Equal("Germany", dashboard.ExportPlan.Destination);
        Assert.NotNull(dashboard.LatestSoilMeasurement);
        Assert.Equal(7.1, dashboard.LatestSoilMeasurement!.Ph, precision: 3);
        Assert.Equal(4, dashboard.TaskSummary.Total);
        Assert.Equal(2, dashboard.TaskSummary.Pending);
        Assert.Equal(1, dashboard.TaskSummary.InProgress);
        Assert.Equal(1, dashboard.TaskSummary.Completed);
        Assert.Contains(dashboard.UpcomingTasks, task => task.Title == "Upcoming irrigation");
        Assert.Contains(dashboard.OverdueTasks, task => task.Title == "Overdue inspection");
        Assert.Contains(dashboard.CompletedTasks, task => task.Title == "Completed soil test");
        Assert.True(dashboard.Season.CurrentSeasonDay > 0);
        Assert.True(dashboard.Season.ProgressPercentage > 0m);
    }

    [Fact]
    public async Task DifferentFarmOwner_CannotAccessDashboard()
    {
        var exportPlanId = await CreateExportPlanWithDashboardDataAsync();

        using var client = CreateFarmOwnerClient(GreenmindApiFactory.OtherFarmOwnerUserId);
        var response = await client.GetAsync($"/api/farm-owner/export-plans/{exportPlanId}/dashboard");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Tasks_FilterByExportPlan_AndNeverLeakAnotherPlan()
    {
        var requestedPlanId = await CreateExportPlanWithDashboardDataAsync();
        int otherPlanId;
        using (var scope = factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<GreenmindDbContext>();
            var farm = await dbContext.Farms.SingleAsync(item =>
                item.OwnerUserId == GreenmindApiFactory.FarmOwnerUserId);
            var today = DateOnly.FromDateTime(DateTime.UtcNow);
            var otherPlan = new ExportPlan
            {
                FarmId = farm.Id,
                CropId = 1,
                DestinationId = 1,
                Name = $"Other plan {Guid.NewGuid():N}",
                Governorate = "Giza",
                City = "Giza",
                AreaInFeddan = 2,
                Latitude = 30,
                Longitude = 31,
                PlantingDate = today,
                ExpectedHarvestDate = today.AddDays(60),
                ExpectedExportDate = today.AddDays(70),
                Status = ExportPlanStatus.InProgress
            };
            dbContext.ExportPlans.Add(otherPlan);
            await dbContext.SaveChangesAsync();
            otherPlanId = otherPlan.Id;
            dbContext.FarmTasks.Add(new FarmTask
            {
                ExportPlanId = otherPlan.Id,
                TaskTypeId = 1,
                Title = "Must not appear",
                PlannedDate = today,
                Status = DomainTaskStatus.Pending,
                Priority = TaskPriority.High
            });
            await dbContext.SaveChangesAsync();
        }

        using var ownerClient = CreateFarmOwnerClient(GreenmindApiFactory.FarmOwnerUserId);
        var response = await ownerClient.GetAsync($"/api/Tasks?exportPlanId={requestedPlanId}&pageSize=100");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var tasks = await ReadDataAsync<List<JsonElement>>(response);
        Assert.NotEmpty(tasks);
        Assert.All(tasks, task => Assert.Equal(requestedPlanId, task.GetProperty("exportPlanId").GetInt32()));
        Assert.DoesNotContain(tasks, task =>
            task.GetProperty("exportPlanId").GetInt32() == otherPlanId ||
            task.GetProperty("title").GetString() == "Must not appear");

        using var otherOwnerClient = CreateFarmOwnerClient(GreenmindApiFactory.OtherFarmOwnerUserId);
        var otherOwnerResponse = await otherOwnerClient.GetAsync($"/api/Tasks?exportPlanId={requestedPlanId}&pageSize=100");
        var otherOwnerTasks = await ReadDataAsync<List<JsonElement>>(otherOwnerResponse);
        Assert.Empty(otherOwnerTasks);
    }

    private async Task<int> CreateExportPlanWithDashboardDataAsync()
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<GreenmindDbContext>();
        var farm = await dbContext.Farms.SingleAsync(farm =>
            farm.OwnerUserId == GreenmindApiFactory.FarmOwnerUserId);

        var exportPlan = new ExportPlan
        {
            FarmId = farm.Id,
            CropId = 1,
            DestinationId = 1,
            Name = $"Dashboard Plan {Guid.NewGuid():N}",
            Governorate = "Giza",
            City = "6th of October",
            AreaInFeddan = 10m,
            Latitude = 29.9,
            Longitude = 30.9,
            PlantingDate = today.AddDays(-10),
            ExpectedHarvestDate = today.AddDays(90),
            ExpectedExportDate = today.AddDays(95),
            Status = ExportPlanStatus.InProgress
        };

        dbContext.ExportPlans.Add(exportPlan);
        await dbContext.SaveChangesAsync();

        dbContext.SoilMeasurements.AddRange(
            new SoilMeasurement
            {
                ExportPlanId = exportPlan.Id,
                MeasurementDate = today.AddDays(-5),
                PH = 6.2,
                EC = 1.4,
                Nitrogen = 12,
                Phosphorus = 8,
                Potassium = 15,
                Moisture = 30
            },
            new SoilMeasurement
            {
                ExportPlanId = exportPlan.Id,
                MeasurementDate = today,
                PH = 7.1,
                EC = 1.7,
                Nitrogen = 18,
                Phosphorus = 12,
                Potassium = 20,
                Moisture = 42,
                Notes = "Latest reading"
            });

        dbContext.FarmTasks.AddRange(
            new FarmTask
            {
                ExportPlanId = exportPlan.Id,
                TaskTypeId = 1,
                Title = "Upcoming irrigation",
                PlannedDate = today.AddDays(2),
                Status = DomainTaskStatus.Pending,
                Priority = TaskPriority.High
            },
            new FarmTask
            {
                ExportPlanId = exportPlan.Id,
                TaskTypeId = 2,
                Title = "In progress fertilization",
                PlannedDate = today.AddDays(1),
                Status = DomainTaskStatus.InProgress,
                Priority = TaskPriority.Medium
            },
            new FarmTask
            {
                ExportPlanId = exportPlan.Id,
                TaskTypeId = 3,
                Title = "Overdue inspection",
                PlannedDate = today.AddDays(-1),
                Status = DomainTaskStatus.Pending,
                Priority = TaskPriority.Critical
            },
            new FarmTask
            {
                ExportPlanId = exportPlan.Id,
                TaskTypeId = 5,
                Title = "Completed soil test",
                PlannedDate = today.AddDays(-2),
                ActualDate = today.AddDays(-1),
                Status = DomainTaskStatus.Completed,
                Priority = TaskPriority.Low
            });

        await dbContext.SaveChangesAsync();
        return exportPlan.Id;
    }

    private HttpClient CreateFarmOwnerClient(string userId)
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Test-Role", RoleNames.FarmOwner);
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
