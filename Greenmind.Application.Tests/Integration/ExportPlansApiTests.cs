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

        var analysis = await AnalyzeScheduleAsync(client, CreateAnalysisRequest());

        var createResponse = await client.PostAsJsonAsync(
            "/api/farm-owner/export-plans",
            CreateRequest(
                plantingDate: analysis.PlantingDate,
                expectedHarvestDate: analysis.ExpectedHarvestDate,
                expectedExportDate: analysis.ExpectedExportDate,
                analysisToken: analysis.AnalysisToken));

        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
        var created = await ReadDataAsync<ExportPlanDetailsDto>(createResponse);
        Assert.Equal("Summer Strawberry Plan", created.Name);
        Assert.Equal("Strawberry", created.Crop);
        Assert.True(created.SupportsSmartDiagnosis);
        Assert.Equal(ExportPlanStatus.AwaitingFarmData.ToString(), created.Status);
        Assert.Equal(PlanningGenerationStatus.Pending.ToString(), created.PlanningStatus);
        Assert.False(created.IsFarmDataReady);
        Assert.Equal(0, created.GeneratedTaskCount);
        Assert.Equal(29.9, created.Latitude);
        Assert.Equal(30.9, created.Longitude);

        var planningResponse = await client.GetAsync(
            $"/api/farm-owner/export-plans/{created.Id}/planning-status");
        var planning = await ReadDataAsync<PlanningStatusDto>(planningResponse);
        Assert.Equal(PlanningGenerationStatus.Pending.ToString(), planning.Status);

        var listResponse = await client.GetAsync("/api/farm-owner/export-plans");
        var plans = await ReadDataAsync<List<ExportPlanListItemDto>>(listResponse);
        Assert.Contains(plans, plan => plan.Id == created.Id && plan.Name == created.Name);

        var detailsResponse = await client.GetAsync($"/api/farm-owner/export-plans/{created.Id}");
        var details = await ReadDataAsync<ExportPlanDetailsDto>(detailsResponse);
        Assert.Equal(created, details);
        Assert.Equal(29.9, details.Latitude);
        Assert.Equal(30.9, details.Longitude);

        // Update farm data to complete status
        var updateFarmResponse = await client.PutAsJsonAsync(
            $"/api/farm-owner/export-plans/{created.Id}/farm-data",
            new ExportPlanFarmDataUpdateRequest
            {
                FarmType = "صوب زراعية",
                SoilType = "طينية",
                IrrigationSystem = "تنقيط",
                Fertigation = "نعم",
                SoilPh = 6.5,
                SoilEc = 1.8,
                SoilNitrogen = 45,
                SoilPhosphorus = 30,
                SoilPotassium = 200,
                SoilOrganicMatter = 2.5,
                WaterPh = 7.0,
                WaterEc = 0.8,
                WaterSalinity = 500,
                WaterSodium = 25,
                WaterChloride = 30,
                WaterBicarbonate = 120,
                WaterCalcium = 40,
                WaterMagnesium = 15,
                WaterSar = 1.2
            });
        Assert.Equal(HttpStatusCode.OK, updateFarmResponse.StatusCode);
        var updatedDetails = await ReadDataAsync<ExportPlanDetailsDto>(updateFarmResponse);
        Assert.True(updatedDetails.IsFarmDataReady);
        Assert.Equal(ExportPlanStatus.Planning.ToString(), updatedDetails.Status);
    }

    [Fact]
    public async Task ScheduleAnalysis_UsesAndBindsExactCoordinates()
    {
        using var client = CreateFarmOwnerClient(GreenmindApiFactory.FarmOwnerUserId);
        var request = CreateAnalysisRequest(latitude: 29.912345, longitude: 30.923456);

        var analysis = await AnalyzeScheduleAsync(client, request);
        Assert.Equal("Feasible", analysis.Assessment);
        Assert.Contains(analysis.Reasons, value => value.Contains("الإحداثيات"));

        var changedCoordinates = CreateRequest(
            plantingDate: analysis.PlantingDate,
            expectedHarvestDate: analysis.ExpectedHarvestDate,
            expectedExportDate: analysis.ExpectedExportDate,
            analysisToken: analysis.AnalysisToken,
            latitude: request.Latitude!.Value + 0.01,
            longitude: request.Longitude!.Value);
        var response = await client.PostAsJsonAsync("/api/farm-owner/export-plans", changedCoordinates);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var envelope = await response.Content.ReadFromJsonAsync<ApiResponse<JsonElement>>();
        Assert.Contains("AnalysisToken", envelope!.Error!.ValidationErrors!.Keys);
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
    public async Task SoftDeletedMasterData_IsRejectedForNewExportPlans()
    {
        var deletedCropId = await AddDeletedCropAsync();
        using var client = CreateFarmOwnerClient(GreenmindApiFactory.FarmOwnerUserId);

        var response = await client.PostAsJsonAsync(
            "/api/farm-owner/export-plans",
            CreateRequest(cropId: deletedCropId));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var envelope = await response.Content.ReadFromJsonAsync<ApiResponse<JsonElement>>();
        Assert.NotNull(envelope);
        Assert.Contains("CropId", envelope.Error!.ValidationErrors!.Keys);
    }

    [Fact]
    public async Task ExistingExportPlanStillShowsSoftDeletedCropAndDestinationNames()
    {
        var plan = await CreateExportPlanWithDeletedMasterDataAsync();
        using var client = CreateFarmOwnerClient(GreenmindApiFactory.FarmOwnerUserId);

        var response = await client.GetAsync($"/api/farm-owner/export-plans/{plan.Id}");
        var details = await ReadDataAsync<ExportPlanDetailsDto>(response);

        Assert.Equal(plan.CropName, details.Crop);
        Assert.Equal(plan.DestinationName, details.Destination);
        Assert.False(details.SupportsSmartDiagnosis);
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

    [Fact]
    public async Task Owner_CanCreateExportPlanWithFullFarmData_StartsInPlanning()
    {
        using var client = CreateFarmOwnerClient(GreenmindApiFactory.FarmOwnerUserId);
        var analysis = await AnalyzeScheduleAsync(client, CreateAnalysisRequest());

        var req = CreateRequest(
            plantingDate: analysis.PlantingDate,
            expectedHarvestDate: analysis.ExpectedHarvestDate,
            expectedExportDate: analysis.ExpectedExportDate,
            analysisToken: analysis.AnalysisToken);

        var fullReq = new ExportPlanCreateRequest
        {
            Name = req.Name,
            CropId = req.CropId,
            DestinationId = req.DestinationId,
            Governorate = req.Governorate,
            City = req.City,
            Latitude = req.Latitude,
            Longitude = req.Longitude,
            AreaInFeddan = req.AreaInFeddan,
            PlantingDate = req.PlantingDate,
            ExpectedHarvestDate = req.ExpectedHarvestDate,
            ExpectedExportDate = req.ExpectedExportDate,
            Notes = req.Notes,
            AnalysisToken = req.AnalysisToken,
            FarmType = "أرض مفتوحة",
            SoilType = "رملية",
            IrrigationSystem = "تنقيط",
            Fertigation = "نعم",
            SoilPh = 7.0,
            SoilEc = 1.5,
            SoilNitrogen = 50,
            SoilPhosphorus = 25,
            SoilPotassium = 180,
            SoilOrganicMatter = 2.0,
            WaterPh = 7.2,
            WaterEc = 0.9,
            WaterSalinity = 600,
            WaterSodium = 30,
            WaterChloride = 35,
            WaterBicarbonate = 140,
            WaterCalcium = 45,
            WaterMagnesium = 18,
            WaterSar = 1.4
        };

        var response = await client.PostAsJsonAsync("/api/farm-owner/export-plans", fullReq);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var created = await ReadDataAsync<ExportPlanDetailsDto>(response);
        Assert.True(created.IsFarmDataReady);
        Assert.Equal(ExportPlanStatus.Planning.ToString(), created.Status);
    }

    [Fact]
    public async Task Owner_CannotStartTasksWhenFarmDataIncomplete()
    {
        using var client = CreateFarmOwnerClient(GreenmindApiFactory.FarmOwnerUserId);
        var analysis = await AnalyzeScheduleAsync(client, CreateAnalysisRequest());

        var createResponse = await client.PostAsJsonAsync(
            "/api/farm-owner/export-plans",
            CreateRequest(
                plantingDate: analysis.PlantingDate,
                expectedHarvestDate: analysis.ExpectedHarvestDate,
                expectedExportDate: analysis.ExpectedExportDate,
                analysisToken: analysis.AnalysisToken));

        var created = await ReadDataAsync<ExportPlanDetailsDto>(createResponse);
        Assert.False(created.IsFarmDataReady);

        var startResponse = await client.PostAsync(
            $"/api/farm-owner/export-plans/{created.Id}/start-tasks",
            null);

        Assert.Equal(HttpStatusCode.BadRequest, startResponse.StatusCode);
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

    private async Task<int> AddDeletedCropAsync()
    {
        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<GreenmindDbContext>();
        var crop = new Crop
        {
            Name = $"Deleted crop {Guid.NewGuid():N}",
            IsActive = true,
            IsDeleted = true,
            DeletedAtUtc = DateTime.UtcNow
        };

        dbContext.Crops.Add(crop);
        await dbContext.SaveChangesAsync();
        return crop.Id;
    }

    private async Task<(int Id, string CropName, string DestinationName)> CreateExportPlanWithDeletedMasterDataAsync()
    {
        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<GreenmindDbContext>();
        var farm = await dbContext.Farms.SingleAsync(item => item.OwnerUserId == GreenmindApiFactory.FarmOwnerUserId);
        var crop = new Crop
        {
            Name = $"Historical crop {Guid.NewGuid():N}",
            IsActive = true
        };
        var destination = new Destination
        {
            Name = $"Historical destination {Guid.NewGuid():N}",
            IsActive = true
        };

        dbContext.Crops.Add(crop);
        dbContext.Destinations.Add(destination);
        await dbContext.SaveChangesAsync();

        var plantingDate = new DateOnly(2026, 1, 10);
        var exportPlan = new ExportPlan
        {
            FarmId = farm.Id,
            CropId = crop.Id,
            DestinationId = destination.Id,
            Name = $"Historical plan {Guid.NewGuid():N}",
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

        crop.IsDeleted = true;
        crop.DeletedAtUtc = DateTime.UtcNow;
        destination.IsDeleted = true;
        destination.DeletedAtUtc = DateTime.UtcNow;
        await dbContext.SaveChangesAsync();

        return (exportPlan.Id, crop.Name, destination.Name);
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
        DateOnly? expectedExportDate = null,
        string? analysisToken = null,
        double latitude = 29.9,
        double longitude = 30.9) =>
        new()
        {
            Name = "Summer Strawberry Plan",
            CropId = cropId,
            DestinationId = 1,
            Governorate = "Giza",
            City = "6th of October",
            Latitude = latitude,
            Longitude = longitude,
            AreaInFeddan = 5.5m,
            PlantingDate = plantingDate ?? new DateOnly(2026, 1, 10),
            ExpectedHarvestDate = expectedHarvestDate ?? new DateOnly(2026, 4, 10),
            ExpectedExportDate = expectedExportDate ?? new DateOnly(2026, 4, 15),
            Status = ExportPlanStatus.Planning,
            Notes = "  First season  ",
            AnalysisToken = analysisToken
        };

    private static ExportPlanScheduleAnalysisRequest CreateAnalysisRequest(
        double latitude = 29.9,
        double longitude = 30.9) => new()
        {
            Name = "Summer Strawberry Plan",
            CropId = 1,
            DestinationId = 1,
            Governorate = "Giza",
            City = "6th of October",
            Latitude = latitude,
            Longitude = longitude,
            AreaInFeddan = 5.5m,
            Notes = "First season"
        };

    private static async Task<ExportPlanScheduleAnalysisDto> AnalyzeScheduleAsync(
        HttpClient client,
        ExportPlanScheduleAnalysisRequest request)
    {
        var response = await client.PostAsJsonAsync(
            "/api/farm-owner/export-plans/schedule-analysis",
            request);
        Assert.True(response.IsSuccessStatusCode, await response.Content.ReadAsStringAsync());
        return await ReadDataAsync<ExportPlanScheduleAnalysisDto>(response);
    }

    private static async Task<T> ReadDataAsync<T>(HttpResponseMessage response)
    {
        Assert.True(response.IsSuccessStatusCode);
        var envelope = await response.Content.ReadFromJsonAsync<ApiResponse<JsonElement>>();
        Assert.NotNull(envelope);
        Assert.True(envelope.Succeeded);
        return envelope.Data.Deserialize<T>(new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
    }
}
