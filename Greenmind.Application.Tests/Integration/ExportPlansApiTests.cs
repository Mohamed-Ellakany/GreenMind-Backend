using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Greenmind.Application.Contracts.Api;
using Greenmind.Application.Dtos.ExportPlans;
using Greenmind.Domain.Consts;
using Greenmind.Domain.Consts.Enums;
using Greenmind.Domain.Entities;
using Greenmind.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Greenmind.Application.Tests.Integration;

public sealed class ExportPlansApiTests(GreenmindApiFactory factory)
    : IClassFixture<GreenmindApiFactory>
{
    [Fact]
    public async Task Owner_CanCreateListAndViewOwnExportPlan()
    {
        using var client = CreateFarmOwnerClient(GreenmindApiFactory.FarmOwnerUserId);

        var createResponse = await client.PostAsJsonAsync(
            "/api/farm-owner/export-plans",
            CreateRequest());

        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
        var created = await ReadDataAsync<ExportPlanDetailsDto>(createResponse);
        Assert.Equal("Summer Strawberry Plan", created.Name);
        Assert.Equal("Strawberry", created.Crop);

        var listResponse = await client.GetAsync("/api/farm-owner/export-plans");
        var plans = await ReadDataAsync<List<ExportPlanListItemDto>>(listResponse);
        Assert.Contains(plans, plan => plan.Id == created.Id && plan.Name == created.Name);

        var detailsResponse = await client.GetAsync($"/api/farm-owner/export-plans/{created.Id}");
        var details = await ReadDataAsync<ExportPlanDetailsDto>(detailsResponse);
        Assert.Equal(created, details);
    }

    [Fact]
    public async Task InvalidRequest_ReturnsFieldLevelBadRequest()
    {
        using var client = CreateFarmOwnerClient(GreenmindApiFactory.FarmOwnerUserId);
        var plantingDate = new DateOnly(2026, 1, 10);

        var rangeValidationResponse = await client.PostAsJsonAsync(
            "/api/farm-owner/export-plans",
            new ExportPlanCreateRequest
            {
                Name = "Invalid Plan",
                CropId = 1,
                DestinationId = 1,
                Governorate = "Giza",
                City = "6th of October",
                Latitude = 91,
                Longitude = 181,
                AreaInFeddan = 0,
                PlantingDate = plantingDate,
                ExpectedHarvestDate = plantingDate.AddDays(1),
                ExpectedExportDate = plantingDate.AddDays(1),
                Status = ExportPlanStatus.Planning
            });

        Assert.Equal(HttpStatusCode.BadRequest, rangeValidationResponse.StatusCode);
        var envelope = await rangeValidationResponse.Content.ReadFromJsonAsync<ApiResponse<JsonElement>>();
        Assert.NotNull(envelope);
        Assert.False(envelope.Succeeded);
        Assert.Contains("Latitude", envelope.Error!.ValidationErrors!.Keys);
        Assert.Contains("Longitude", envelope.Error.ValidationErrors.Keys);
        Assert.Contains("AreaInFeddan", envelope.Error.ValidationErrors.Keys);

        var dateValidationResponse = await client.PostAsJsonAsync(
            "/api/farm-owner/export-plans",
            CreateRequest(
                plantingDate: plantingDate,
                expectedHarvestDate: plantingDate,
                expectedExportDate: plantingDate.AddDays(-1)));

        Assert.Equal(HttpStatusCode.BadRequest, dateValidationResponse.StatusCode);
        var dateEnvelope = await dateValidationResponse.Content.ReadFromJsonAsync<ApiResponse<JsonElement>>();
        Assert.NotNull(dateEnvelope);
        Assert.Contains("ExpectedHarvestDate", dateEnvelope.Error!.ValidationErrors!.Keys);
        Assert.Contains("ExpectedExportDate", dateEnvelope.Error.ValidationErrors.Keys);
    }

    [Fact]
    public async Task InactiveMasterData_IsRejected()
    {
        var inactiveCropId = await AddInactiveCropAsync();
        using var client = CreateFarmOwnerClient(GreenmindApiFactory.FarmOwnerUserId);

        var response = await client.PostAsJsonAsync(
            "/api/farm-owner/export-plans",
            CreateRequest(cropId: inactiveCropId));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var envelope = await response.Content.ReadFromJsonAsync<ApiResponse<JsonElement>>();
        Assert.NotNull(envelope);
        Assert.Contains("CropId", envelope.Error!.ValidationErrors!.Keys);
    }

    [Fact]
    public async Task DifferentFarmOwner_CannotListOrViewAnotherOwnersExportPlan()
    {
        var otherPlanId = await CreateExportPlanAsync(GreenmindApiFactory.OtherFarmOwnerUserId);
        using var client = CreateFarmOwnerClient(GreenmindApiFactory.FarmOwnerUserId);

        var listResponse = await client.GetAsync("/api/farm-owner/export-plans");
        var plans = await ReadDataAsync<List<ExportPlanListItemDto>>(listResponse);
        var detailsResponse = await client.GetAsync($"/api/farm-owner/export-plans/{otherPlanId}");

        Assert.DoesNotContain(plans, plan => plan.Id == otherPlanId);
        Assert.Equal(HttpStatusCode.NotFound, detailsResponse.StatusCode);
    }

    private async Task<int> AddInactiveCropAsync()
    {
        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<GreenmindDbContext>();
        var crop = new Crop
        {
            Name = $"Inactive crop {Guid.NewGuid():N}",
            IsActive = false
        };

        dbContext.Crops.Add(crop);
        await dbContext.SaveChangesAsync();
        return crop.Id;
    }

    private async Task<int> CreateExportPlanAsync(string ownerUserId)
    {
        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<GreenmindDbContext>();
        var farm = await dbContext.Farms.SingleAsync(item => item.OwnerUserId == ownerUserId);
        var plantingDate = new DateOnly(2026, 1, 10);
        var exportPlan = new ExportPlan
        {
            FarmId = farm.Id,
            CropId = 1,
            DestinationId = 1,
            Name = $"Other owner plan {Guid.NewGuid():N}",
            Governorate = "Giza",
            City = "6th of October",
            Latitude = 29.9,
            Longitude = 30.9,
            AreaInFeddan = 5m,
            PlantingDate = plantingDate,
            ExpectedHarvestDate = plantingDate.AddDays(90),
            ExpectedExportDate = plantingDate.AddDays(95),
            Status = ExportPlanStatus.Planning
        };

        dbContext.ExportPlans.Add(exportPlan);
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

    private static ExportPlanCreateRequest CreateRequest(
        int cropId = 1,
        DateOnly? plantingDate = null,
        DateOnly? expectedHarvestDate = null,
        DateOnly? expectedExportDate = null) =>
        new()
        {
            Name = "Summer Strawberry Plan",
            CropId = cropId,
            DestinationId = 1,
            Governorate = "Giza",
            City = "6th of October",
            Latitude = 29.9,
            Longitude = 30.9,
            AreaInFeddan = 5.5m,
            PlantingDate = plantingDate ?? new DateOnly(2026, 1, 10),
            ExpectedHarvestDate = expectedHarvestDate ?? new DateOnly(2026, 4, 10),
            ExpectedExportDate = expectedExportDate ?? new DateOnly(2026, 4, 15),
            Status = ExportPlanStatus.Planning,
            Notes = "  First season  "
        };

    private static async Task<T> ReadDataAsync<T>(HttpResponseMessage response)
    {
        Assert.True(response.IsSuccessStatusCode);
        var envelope = await response.Content.ReadFromJsonAsync<ApiResponse<JsonElement>>();
        Assert.NotNull(envelope);
        Assert.True(envelope.Succeeded);
        return envelope.Data.Deserialize<T>(new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
    }
}
