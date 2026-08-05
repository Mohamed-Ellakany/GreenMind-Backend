using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Greenmind.Api.Json;
using Greenmind.Application.Contracts.Api;
using Greenmind.Application.Dtos.Support;
using Greenmind.Domain.Consts;
using Greenmind.Domain.Consts.Enums;
using Greenmind.Domain.Entities;
using Greenmind.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;

namespace Greenmind.Application.Tests.Integration;

public sealed class SupportAndAccountApiTests(GreenmindApiFactory factory)
    : IClassFixture<GreenmindApiFactory>
{
    private static readonly JsonSerializerOptions JsonOptions = CreateJsonOptions();

    [Fact]
    public async Task FarmOwner_CanUpdateFarmNameFromAccountSettings()
    {
        using var owner = CreateClient(RoleNames.FarmOwner, GreenmindApiFactory.FarmOwnerUserId);

        var response = await owner.PutAsJsonAsync("/api/Auth/me/farm", new
        {
            farmName = "مزرعة الفراولة للتصدير"
        });
        var profile = (await response.Content.ReadFromJsonAsync<ApiResponse<AccountProfileResponse>>(JsonOptions))?.Data;

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("مزرعة الفراولة للتصدير", profile?.FarmName);

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<GreenmindDbContext>();
        Assert.True(await db.Farms.AnyAsync(item =>
            item.OwnerUserId == GreenmindApiFactory.FarmOwnerUserId &&
            item.Name == "مزرعة الفراولة للتصدير"));
    }

    [Fact]
    public async Task FarmNameUpdate_RejectsBlankName()
    {
        using var owner = CreateClient(RoleNames.FarmOwner, GreenmindApiFactory.FarmOwnerUserId);

        var response = await owner.PutAsJsonAsync("/api/Auth/me/farm", new { farmName = "   " });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public void SupportConversation_AllowsOnlyOneOpenConversationPerFarm()
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<GreenmindDbContext>();
        var entityType = db.Model.FindEntityType(typeof(SupportConversation));
        var farmIndex = entityType?.GetIndexes()
            .SingleOrDefault(index =>
                index.IsUnique &&
                index.Properties.Count == 1 &&
                index.Properties[0].Name == nameof(SupportConversation.FarmId));

        Assert.NotNull(farmIndex);
        Assert.Equal("[IsClosed] = 0", farmIndex.GetFilter());
    }

    [Fact]
    public async Task SupportConversation_AllowsCustomerAndAdminMessagesOnlyForTheOwningFarm()
    {
        using var owner = CreateClient(RoleNames.FarmOwner, GreenmindApiFactory.FarmOwnerUserId);
        using var otherOwner = CreateClient(RoleNames.FarmOwner, GreenmindApiFactory.OtherFarmOwnerUserId);
        using var admin = CreateClient(RoleNames.Admin, GreenmindApiFactory.AdminUserId);

        var conversationResponse = await owner.GetAsync("/api/support/conversation");
        var conversation = (await conversationResponse.Content
            .ReadFromJsonAsync<ApiResponse<SupportConversationDto>>(JsonOptions))?.Data;
        Assert.Equal(HttpStatusCode.OK, conversationResponse.StatusCode);
        Assert.NotNull(conversation);

        var customerMessageResponse = await owner.PostAsJsonAsync(
            "/api/support/conversation/messages",
            new { content = "محتاج مساعدة في مراجعة خطة التصدير." });
        var customerMessage = (await customerMessageResponse.Content
            .ReadFromJsonAsync<ApiResponse<SupportMessageDto>>(JsonOptions))?.Data;
        Assert.Equal(HttpStatusCode.OK, customerMessageResponse.StatusCode);
        Assert.Equal(SupportMessageRole.Customer, customerMessage?.Role);

        var hiddenFromOtherOwner = await otherOwner.GetAsync("/api/support/conversation/messages");
        var otherOwnerMessages = (await hiddenFromOtherOwner.Content
            .ReadFromJsonAsync<ApiResponse<SupportMessagePageDto>>(JsonOptions))?.Data;
        Assert.Equal(HttpStatusCode.OK, hiddenFromOtherOwner.StatusCode);
        Assert.Empty(otherOwnerMessages?.Items ?? []);

        var adminList = await admin.GetAsync("/api/admin/support/conversations");
        var conversations = (await adminList.Content
            .ReadFromJsonAsync<ApiResponse<IReadOnlyList<SupportConversationDto>>>(JsonOptions))?.Data;
        Assert.Contains(conversations ?? [], item => item.Id == conversation!.Id);

        var adminReplyResponse = await admin.PostAsJsonAsync(
            $"/api/admin/support/conversations/{conversation!.Id}/messages",
            new { content = "تم استلام الرسالة، هنراجعها ونرد عليك هنا." });
        var adminReply = (await adminReplyResponse.Content
            .ReadFromJsonAsync<ApiResponse<SupportMessageDto>>(JsonOptions))?.Data;
        Assert.Equal(HttpStatusCode.OK, adminReplyResponse.StatusCode);
        Assert.Equal(SupportMessageRole.Admin, adminReply?.Role);

        var ownerMessages = await owner.GetAsync("/api/support/conversation/messages");
        var page = (await ownerMessages.Content
            .ReadFromJsonAsync<ApiResponse<SupportMessagePageDto>>(JsonOptions))?.Data;
        Assert.Equal(2, page?.TotalCount);
        Assert.Contains(page?.Items ?? [], item => item.Role == SupportMessageRole.Admin);
    }

    private HttpClient CreateClient(string role, string userId)
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Test-Role", role);
        client.DefaultRequestHeaders.Add("X-Test-UserId", userId);
        return client;
    }

    private static JsonSerializerOptions CreateJsonOptions()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        options.Converters.Add(new ArabicBusinessEnumJsonConverterFactory());
        options.Converters.Add(new JsonStringEnumConverter());
        return options;
    }

    private sealed record AccountProfileResponse(
        string FullName,
        string? Email,
        string? FarmName,
        int? FarmId,
        bool IsActive,
        bool? IsFarmApproved);
}
