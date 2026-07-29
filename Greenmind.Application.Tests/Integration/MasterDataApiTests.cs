using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Greenmind.Application.Contracts.Api;
using Greenmind.Application.Dtos.MasterData;
using Greenmind.Domain.Consts;

namespace Greenmind.Application.Tests.Integration;

public sealed class MasterDataApiTests(GreenmindApiFactory factory)
    : IClassFixture<GreenmindApiFactory>
{
    [Fact]
    public async Task AnonymousUser_CannotAccessAdminEndpoints()
    {
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync(
            "/api/admin/crops",
            new MasterDataRequest("Anonymous Crop", null));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task FarmOwner_CannotMutateMasterData()
    {
        using var client = CreateAuthenticatedClient(RoleNames.FarmOwner);

        var response = await client.PostAsJsonAsync(
            "/api/admin/destinations",
            new MasterDataRequest("Owner Destination", null));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Admin_CanCreateUpdateListAndDeactivateCrop()
    {
        using var client = CreateAuthenticatedClient(RoleNames.Admin);
        var name = $"Crop-{Guid.NewGuid():N}";

        var createResponse = await client.PostAsJsonAsync(
            "/api/admin/crops",
            new MasterDataRequest(name, "Created by integration test"));
        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);

        var created = await ReadDataAsync<MasterDataItemDto>(createResponse);
        var updatedName = $"{name}-updated";
        var updateResponse = await client.PutAsJsonAsync(
            $"/api/admin/crops/{created.Id}",
            new MasterDataRequest(updatedName, "Updated"));
        Assert.Equal(HttpStatusCode.OK, updateResponse.StatusCode);

        var statusResponse = await client.PatchAsJsonAsync(
            $"/api/admin/crops/{created.Id}/status",
            new MasterDataStatusRequest(false));
        Assert.Equal(HttpStatusCode.OK, statusResponse.StatusCode);

        var listResponse = await client.GetAsync("/api/admin/crops");
        var items = await ReadDataAsync<List<MasterDataItemDto>>(listResponse);
        Assert.Contains(items, item => item.Id == created.Id && item.Name == updatedName && !item.IsActive);
    }

    [Theory]
    [InlineData("crops")]
    [InlineData("destinations")]
    public async Task Admin_CanSoftDeleteAndRestoreMasterData(string resource)
    {
        using var adminClient = CreateAuthenticatedClient(RoleNames.Admin);
        var name = $"Deleted-{resource}-{Guid.NewGuid():N}";

        var createResponse = await adminClient.PostAsJsonAsync(
            $"/api/admin/{resource}",
            new MasterDataRequest(name, null));
        var created = await ReadDataAsync<MasterDataItemDto>(createResponse);

        var deleteResponse = await adminClient.DeleteAsync($"/api/admin/{resource}/{created.Id}");
        var deleted = await ReadDataAsync<MasterDataItemDto>(deleteResponse);
        Assert.True(deleted.IsDeleted);
        Assert.NotNull(deleted.DeletedAtUtc);

        var defaultListResponse = await adminClient.GetAsync($"/api/admin/{resource}");
        var defaultItems = await ReadDataAsync<List<MasterDataItemDto>>(defaultListResponse);
        Assert.DoesNotContain(defaultItems, item => item.Id == created.Id);

        var deletedListResponse = await adminClient.GetAsync($"/api/admin/{resource}?includeDeleted=true");
        var deletedItems = await ReadDataAsync<List<MasterDataItemDto>>(deletedListResponse);
        Assert.Contains(deletedItems, item => item.Id == created.Id && item.IsDeleted);

        using var ownerClient = CreateAuthenticatedClient(RoleNames.FarmOwner);
        var ownerResponse = await ownerClient.GetAsync($"/api/farm-owner/{resource}");
        var ownerItems = await ReadDataAsync<List<MasterDataItemDto>>(ownerResponse);
        Assert.DoesNotContain(ownerItems, item => item.Id == created.Id);

        var restoreResponse = await adminClient.PostAsJsonAsync(
            $"/api/admin/{resource}/{created.Id}/restore",
            new { });
        var restored = await ReadDataAsync<MasterDataItemDto>(restoreResponse);
        Assert.False(restored.IsDeleted);
        Assert.Null(restored.DeletedAtUtc);
    }

    [Theory]
    [InlineData("crops")]
    [InlineData("destinations")]
    public async Task RestoreDeletedMasterData_FailsWhenNameNowConflicts(string resource)
    {
        using var adminClient = CreateAuthenticatedClient(RoleNames.Admin);
        var name = $"Restore-conflict-{resource}-{Guid.NewGuid():N}";

        var firstResponse = await adminClient.PostAsJsonAsync(
            $"/api/admin/{resource}",
            new MasterDataRequest(name, null));
        var first = await ReadDataAsync<MasterDataItemDto>(firstResponse);
        await adminClient.DeleteAsync($"/api/admin/{resource}/{first.Id}");

        var secondResponse = await adminClient.PostAsJsonAsync(
            $"/api/admin/{resource}",
            new MasterDataRequest(name, null));
        Assert.Equal(HttpStatusCode.Created, secondResponse.StatusCode);

        var restoreResponse = await adminClient.PostAsJsonAsync(
            $"/api/admin/{resource}/{first.Id}/restore",
            new { });

        Assert.Equal(HttpStatusCode.Conflict, restoreResponse.StatusCode);
    }

    [Fact]
    public async Task Admin_CanCreateUpdateListAndDeactivateDestination()
    {
        using var client = CreateAuthenticatedClient(RoleNames.Admin);
        var name = $"Destination-{Guid.NewGuid():N}";

        var createResponse = await client.PostAsJsonAsync(
            "/api/admin/destinations",
            new MasterDataRequest(name, null));
        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);

        var created = await ReadDataAsync<MasterDataItemDto>(createResponse);
        var updatedName = $"{name}-updated";
        var updateResponse = await client.PutAsJsonAsync(
            $"/api/admin/destinations/{created.Id}",
            new MasterDataRequest(updatedName, "Updated"));
        Assert.Equal(HttpStatusCode.OK, updateResponse.StatusCode);

        var statusResponse = await client.PatchAsJsonAsync(
            $"/api/admin/destinations/{created.Id}/status",
            new MasterDataStatusRequest(false));
        Assert.Equal(HttpStatusCode.OK, statusResponse.StatusCode);

        var listResponse = await client.GetAsync("/api/admin/destinations");
        var items = await ReadDataAsync<List<MasterDataItemDto>>(listResponse);
        Assert.Contains(items, item => item.Id == created.Id && item.Name == updatedName && !item.IsActive);
    }

    [Theory]
    [InlineData("crops")]
    [InlineData("destinations")]
    public async Task DuplicateNames_AreRejectedIgnoringCase(string resource)
    {
        using var client = CreateAuthenticatedClient(RoleNames.Admin);
        var name = $"Unique-{Guid.NewGuid():N}";

        var firstResponse = await client.PostAsJsonAsync(
            $"/api/admin/{resource}",
            new MasterDataRequest(name, null));
        var duplicateResponse = await client.PostAsJsonAsync(
            $"/api/admin/{resource}",
            new MasterDataRequest(name.ToUpperInvariant(), null));

        Assert.Equal(HttpStatusCode.Created, firstResponse.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, duplicateResponse.StatusCode);
    }

    [Theory]
    [InlineData("crops")]
    [InlineData("destinations")]
    public async Task FarmOwner_ListsActiveRecordsOnly(string resource)
    {
        using var adminClient = CreateAuthenticatedClient(RoleNames.Admin);
        var activeName = $"Active-{resource}-{Guid.NewGuid():N}";
        var inactiveName = $"Inactive-{resource}-{Guid.NewGuid():N}";

        var activeResponse = await adminClient.PostAsJsonAsync(
            $"/api/admin/{resource}",
            new MasterDataRequest(activeName, null));
        var inactiveResponse = await adminClient.PostAsJsonAsync(
            $"/api/admin/{resource}",
            new MasterDataRequest(inactiveName, null));

        var inactiveItem = await ReadDataAsync<MasterDataItemDto>(inactiveResponse);
        await adminClient.PatchAsJsonAsync(
            $"/api/admin/{resource}/{inactiveItem.Id}/status",
            new MasterDataStatusRequest(false));

        using var ownerClient = CreateAuthenticatedClient(RoleNames.FarmOwner);
        var ownerResponse = await ownerClient.GetAsync($"/api/farm-owner/{resource}");
        var ownerItems = await ReadDataAsync<List<MasterDataItemDto>>(ownerResponse);

        Assert.Equal(HttpStatusCode.Created, activeResponse.StatusCode);
        Assert.Equal(HttpStatusCode.OK, ownerResponse.StatusCode);
        Assert.Contains(ownerItems, item => item.Name == activeName && item.IsActive);
        Assert.DoesNotContain(ownerItems, item => item.Name == inactiveName);
        Assert.All(ownerItems, item => Assert.True(item.IsActive));
    }

    private HttpClient CreateAuthenticatedClient(string role)
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Test-Role", role);
        client.DefaultRequestHeaders.Add(
            "X-Test-UserId",
            role == RoleNames.Admin
                ? GreenmindApiFactory.AdminUserId
                : GreenmindApiFactory.FarmOwnerUserId);
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
