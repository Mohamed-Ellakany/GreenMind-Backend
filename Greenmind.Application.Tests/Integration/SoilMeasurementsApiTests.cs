using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Greenmind.Application.Contracts.Api;
using Greenmind.Application.Dtos.Soil;
using Greenmind.Domain.Consts;
using Greenmind.Domain.Consts.Enums;
using Greenmind.Domain.Entities;
using Greenmind.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Greenmind.Application.Tests.Integration;

public sealed class SoilMeasurementsApiTests(GreenmindApiFactory factory)
    : IClassFixture<GreenmindApiFactory>
{
    [Fact]
    public async Task Owner_CanCreateUpdateListGetLatestAndPermanentlyDeleteMeasurements()
    {
        var exportPlanId = await CreateExportPlanAsync(GreenmindApiFactory.FarmOwnerUserId);
        using var client = CreateFarmOwnerClient(GreenmindApiFactory.FarmOwnerUserId);
        var route = $"/api/farm-owner/export-plans/{exportPlanId}/soil-measurements";

        var olderResponse = await client.PostAsJsonAsync(route, CreateRequest(DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-2)));
        var latestResponse = await client.PostAsJsonAsync(route, CreateRequest(DateOnly.FromDateTime(DateTime.UtcNow), ph: 7.2));

        Assert.Equal(HttpStatusCode.Created, olderResponse.StatusCode);
        Assert.Equal(HttpStatusCode.Created, latestResponse.StatusCode);

        var latestCreated = await ReadDataAsync<SoilMeasurementDto>(latestResponse);
        var updateResponse = await client.PutAsJsonAsync(
            $"{route}/{latestCreated.Id}",
            CreateRequest(latestCreated.MeasurementDate, ph: 6.8, notes: "Updated reading"));
        Assert.Equal(HttpStatusCode.OK, updateResponse.StatusCode);

        var latestQueryResponse = await client.GetAsync($"{route}/latest");
        var latest = await ReadDataAsync<SoilMeasurementDto>(latestQueryResponse);
        Assert.Equal(latestCreated.Id, latest.Id);
        Assert.Equal(6.8, latest.Ph, precision: 3);
        Assert.Equal("Updated reading", latest.Notes);

        var deleteResponse = await client.DeleteAsync($"{route}/{latestCreated.Id}");
        Assert.Equal(HttpStatusCode.OK, deleteResponse.StatusCode);

        var listResponse = await client.GetAsync(route);
        var measurements = await ReadDataAsync<List<SoilMeasurementDto>>(listResponse);
        Assert.Single(measurements);
        Assert.DoesNotContain(measurements, item => item.Id == latestCreated.Id);
    }

    [Fact]
    public async Task InvalidValues_ReturnFieldLevelBadRequest()
    {
        var exportPlanId = await CreateExportPlanAsync(GreenmindApiFactory.FarmOwnerUserId);
        using var client = CreateFarmOwnerClient(GreenmindApiFactory.FarmOwnerUserId);

        var invalidRequest = new SoilMeasurementRequest
        {
            MeasurementDate = DateOnly.FromDateTime(DateTime.UtcNow),
            Ph = 14.1,
            Ec = -0.1,
            Nitrogen = -1,
            Phosphorus = 5,
            Potassium = 6,
            Moisture = -2
        };

        var response = await client.PostAsJsonAsync(
            $"/api/farm-owner/export-plans/{exportPlanId}/soil-measurements",
            invalidRequest);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var envelope = await response.Content.ReadFromJsonAsync<ApiResponse<JsonElement>>();
        Assert.NotNull(envelope);
        Assert.False(envelope.Succeeded);
        Assert.Contains("Ph", envelope.Error!.ValidationErrors!.Keys);
        Assert.Contains("Ec", envelope.Error.ValidationErrors.Keys);
        Assert.Contains("Nitrogen", envelope.Error.ValidationErrors.Keys);
        Assert.Contains("Moisture", envelope.Error.ValidationErrors.Keys);
    }

    [Fact]
    public async Task DifferentFarmOwner_CannotListUpdateOrDeleteMeasurements()
    {
        var exportPlanId = await CreateExportPlanAsync(GreenmindApiFactory.FarmOwnerUserId);
        using var ownerClient = CreateFarmOwnerClient(GreenmindApiFactory.FarmOwnerUserId);
        var route = $"/api/farm-owner/export-plans/{exportPlanId}/soil-measurements";
        var createResponse = await ownerClient.PostAsJsonAsync(
            route,
            CreateRequest(DateOnly.FromDateTime(DateTime.UtcNow)));
        var measurement = await ReadDataAsync<SoilMeasurementDto>(createResponse);

        using var otherOwnerClient = CreateFarmOwnerClient(GreenmindApiFactory.OtherFarmOwnerUserId);
        var listResponse = await otherOwnerClient.GetAsync(route);
        var updateResponse = await otherOwnerClient.PutAsJsonAsync(
            $"{route}/{measurement.Id}",
            CreateRequest(measurement.MeasurementDate, ph: 7));
        var deleteResponse = await otherOwnerClient.DeleteAsync($"{route}/{measurement.Id}");

        Assert.Equal(HttpStatusCode.NotFound, listResponse.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, updateResponse.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, deleteResponse.StatusCode);
    }

    private async Task<int> CreateExportPlanAsync(string ownerUserId)
    {
        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<GreenmindDbContext>();
        var farm = await dbContext.Farms.SingleAsync(item => item.OwnerUserId == ownerUserId);
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var exportPlan = new ExportPlan
        {
            FarmId = farm.Id,
            CropId = 1,
            DestinationId = 1,
            Name = $"Soil Plan {Guid.NewGuid():N}",
            Governorate = "Giza",
            City = "6th of October",
            AreaInFeddan = 5m,
            Latitude = 29.9,
            Longitude = 30.9,
            PlantingDate = today,
            ExpectedHarvestDate = today.AddDays(90),
            ExpectedExportDate = today.AddDays(95),
            Status = ExportPlanStatus.InProgress
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

    private static SoilMeasurementRequest CreateRequest(
        DateOnly measurementDate,
        double ph = 6.5,
        string? notes = "Initial reading") =>
        new()
        {
            MeasurementDate = measurementDate,
            Ph = ph,
            Ec = 1.2,
            Nitrogen = 10,
            Phosphorus = 8,
            Potassium = 15,
            Moisture = 35,
            Notes = notes
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
