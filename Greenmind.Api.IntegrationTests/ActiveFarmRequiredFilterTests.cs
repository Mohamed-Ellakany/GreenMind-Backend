using System.Net;
using Greenmind.Domain.Consts;
using Greenmind.Domain.Consts.Enums;

namespace Greenmind.Api.IntegrationTests;

public class ActiveFarmRequiredFilterTests : IClassFixture<GreenmindWebApplicationFactory>, IAsyncLifetime
{
    private readonly GreenmindWebApplicationFactory _factory;

    public ActiveFarmRequiredFilterTests(GreenmindWebApplicationFactory factory)
    {
        _factory = factory;
    }

    public async Task InitializeAsync()
    {
        await _factory.ResetDatabaseAsync();
    }

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task ProtectedEndpoint_WhenFarmIsInactive_Returns403Forbidden()
    {
        var (owner, _) = await _factory.SeedFarmAsync(FarmStatus.Inactive, ownerIsActive: false);

        using var client = _factory.CreateAuthenticatedClient(owner.Id, AppRoles.FarmOwner);

        var response = await client.GetAsync("/api/test/farm-owner/protected");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task ProtectedEndpoint_WhenFarmIsPending_Returns403Forbidden()
    {
        var (owner, _) = await _factory.SeedFarmAsync(FarmStatus.Pending, ownerIsActive: false);

        using var client = _factory.CreateAuthenticatedClient(owner.Id, AppRoles.FarmOwner);

        var response = await client.GetAsync("/api/test/farm-owner/protected");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task ProtectedEndpoint_WhenFarmIsActiveAndOwnerIsActive_Returns200Ok()
    {
        var (owner, _) = await _factory.SeedFarmAsync(FarmStatus.Active, ownerIsActive: true);

        using var client = _factory.CreateAuthenticatedClient(owner.Id, AppRoles.FarmOwner);

        var response = await client.GetAsync("/api/test/farm-owner/protected");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task ProtectedEndpoint_WhenOwnerIsInactive_Returns403Forbidden()
    {
        var (owner, _) = await _factory.SeedFarmAsync(FarmStatus.Active, ownerIsActive: false);

        using var client = _factory.CreateAuthenticatedClient(owner.Id, AppRoles.FarmOwner);

        var response = await client.GetAsync("/api/test/farm-owner/protected");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task ProtectedEndpoint_WhenUserIsPlatformAdmin_BypassesFilter()
    {
        await _factory.SeedFarmAsync(FarmStatus.Inactive, ownerIsActive: false);

        using var client = _factory.CreateAuthenticatedClient(1, AppRoles.PlatformAdmin);

        var response = await client.GetAsync("/api/test/farm-owner/protected");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
