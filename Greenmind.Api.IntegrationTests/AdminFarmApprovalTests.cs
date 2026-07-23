using System.Net;
using System.Net.Http.Json;
using Greenmind.Application.Dtos.Admin;
using Greenmind.Domain.Consts;
using Greenmind.Domain.Consts.Enums;

namespace Greenmind.Api.IntegrationTests;

public class AdminFarmApprovalTests : IClassFixture<GreenmindWebApplicationFactory>, IAsyncLifetime
{
    private readonly GreenmindWebApplicationFactory _factory;

    public AdminFarmApprovalTests(GreenmindWebApplicationFactory factory)
    {
        _factory = factory;
    }

    public async Task InitializeAsync()
    {
        await _factory.ResetDatabaseAsync();
    }

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task GetPendingRegistrations_ReturnsOnlyPendingFarms()
    {
        await _factory.SeedFarmAsync(FarmStatus.Pending, farmName: "Pending Farm A");
        await _factory.SeedFarmAsync(FarmStatus.Pending, farmName: "Pending Farm B");
        await _factory.SeedFarmAsync(FarmStatus.Active, ownerIsActive: true, farmName: "Active Farm");

        using var client = _factory.CreateAuthenticatedClient(1, AppRoles.PlatformAdmin);

        var response = await client.GetAsync("/api/admin/farms/pending");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var farms = await response.Content.ReadFromJsonAsync<List<PendingFarmDto>>();

        Assert.NotNull(farms);
        Assert.Equal(2, farms.Count);
        Assert.All(farms, farm => Assert.Contains("Pending Farm", farm.Name));
    }

    [Fact]
    public async Task ApproveFarm_SetsFarmAndOwnerActive()
    {
        var (_, farm) = await _factory.SeedFarmAsync(FarmStatus.Pending, farmName: "Farm To Approve");

        using var client = _factory.CreateAuthenticatedClient(1, AppRoles.PlatformAdmin);

        var response = await client.PostAsync($"/api/admin/farms/{farm.Id}/approve", null);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var result = await response.Content.ReadFromJsonAsync<FarmActionResultDto>();

        Assert.NotNull(result);
        Assert.Equal(farm.Id, result.FarmId);
        Assert.Equal("Active", result.Status);
        Assert.True(result.OwnerIsActive);
    }

    [Fact]
    public async Task ApproveFarm_WhenAlreadyApproved_Returns409Conflict()
    {
        var (_, farm) = await _factory.SeedFarmAsync(FarmStatus.Active, ownerIsActive: true, farmName: "Already Active");

        using var client = _factory.CreateAuthenticatedClient(1, AppRoles.PlatformAdmin);

        var response = await client.PostAsync($"/api/admin/farms/{farm.Id}/approve", null);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task ApproveFarm_WhenNotFound_Returns404NotFound()
    {
        using var client = _factory.CreateAuthenticatedClient(1, AppRoles.PlatformAdmin);

        var response = await client.PostAsync("/api/admin/farms/99999/approve", null);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task ActivateFarm_ReactivatesInactiveFarm()
    {
        var (_, farm) = await _factory.SeedFarmAsync(FarmStatus.Inactive, farmName: "Inactive Farm");

        using var client = _factory.CreateAuthenticatedClient(1, AppRoles.PlatformAdmin);

        var response = await client.PostAsync($"/api/admin/farms/{farm.Id}/activate", null);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var result = await response.Content.ReadFromJsonAsync<FarmActionResultDto>();

        Assert.NotNull(result);
        Assert.Equal("Active", result.Status);
        Assert.True(result.OwnerIsActive);
    }

    [Fact]
    public async Task ActivateFarm_WhenPending_Returns400BadRequest()
    {
        var (_, farm) = await _factory.SeedFarmAsync(FarmStatus.Pending, farmName: "Pending Farm");

        using var client = _factory.CreateAuthenticatedClient(1, AppRoles.PlatformAdmin);

        var response = await client.PostAsync($"/api/admin/farms/{farm.Id}/activate", null);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task DeactivateFarm_SetsFarmAndOwnerInactive()
    {
        var (_, farm) = await _factory.SeedFarmAsync(FarmStatus.Active, ownerIsActive: true, farmName: "Active Farm");

        using var client = _factory.CreateAuthenticatedClient(1, AppRoles.PlatformAdmin);

        var response = await client.PostAsync($"/api/admin/farms/{farm.Id}/deactivate", null);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var result = await response.Content.ReadFromJsonAsync<FarmActionResultDto>();

        Assert.NotNull(result);
        Assert.Equal("Inactive", result.Status);
        Assert.False(result.OwnerIsActive);
    }
}
