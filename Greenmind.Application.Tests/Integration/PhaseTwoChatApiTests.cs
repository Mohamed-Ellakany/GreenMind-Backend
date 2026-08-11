using System.Net;
using System.Net.Http.Json;
using Greenmind.Application.Contracts.Api;
using Greenmind.Application.Dtos.Chat;
using Greenmind.Domain.Consts;
using Greenmind.Domain.Consts.Enums;
using Greenmind.Domain.Entities;
using Greenmind.Infrastructure.Persistence;
using Greenmind.Api.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Greenmind.Application.Tests.Integration;

public sealed class PhaseTwoChatApiTests(GreenmindApiFactory factory)
    : IClassFixture<GreenmindApiFactory>
{
    private static readonly System.Text.Json.JsonSerializerOptions JsonOptions = CreateJsonOptions();

    [Fact]
    public async Task PlanChat_IsTenantIsolated_Idempotent_AndPersistsStreamedReply()
    {
        var (ownedPlanId, _) = await AddPlansAsync();
        var ownerClient = CreateOwnerClient(GreenmindApiFactory.FarmOwnerUserId);
        var otherOwnerClient = CreateOwnerClient(GreenmindApiFactory.OtherFarmOwnerUserId);

        var createResponse = await ownerClient.PostAsJsonAsync(
            $"/api/export-plans/{ownedPlanId}/chat/sessions",
            new CreateChatSessionRequest { Title = "استشارة الخطة" });
        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);

        var created = await createResponse.Content
            .ReadFromJsonAsync<ApiResponse<ChatSessionDto>>(JsonOptions);
        Assert.NotNull(created?.Data);
        Assert.Equal(ChatScope.ExportPlan, created!.Data!.Scope);

        var forbiddenCreate = await otherOwnerClient.PostAsJsonAsync(
            $"/api/export-plans/{ownedPlanId}/chat/sessions",
            new CreateChatSessionRequest { Title = "محاولة غير مصرح بها" });
        Assert.Equal(HttpStatusCode.NotFound, forbiddenCreate.StatusCode);

        var request = new SendChatMessageRequest
        {
            Content = "ما حالة الخطة؟",
            IdempotencyKey = Guid.NewGuid().ToString()
        };

        var streamUrl =
            $"/api/export-plans/{ownedPlanId}/chat/sessions/{created.Data.Id}/messages/stream";
        var streamResponses = await Task.WhenAll(
            ownerClient.PostAsJsonAsync(streamUrl, request),
            ownerClient.PostAsJsonAsync(streamUrl, request));
        Assert.All(streamResponses, response =>
        {
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Equal("text/event-stream", response.Content.Headers.ContentType?.MediaType);
        });

        var streamBodies = await Task.WhenAll(
            streamResponses.Select(response => response.Content.ReadAsStringAsync()));
        Assert.Contains(streamBodies, body => body.Contains("event: delta"));
        Assert.All(streamBodies, body => Assert.Contains("event: completed", body));

        var replayResponse = await ownerClient.PostAsJsonAsync(
            streamUrl,
            request);
        Assert.Equal(HttpStatusCode.OK, replayResponse.StatusCode);

        var historyResponse = await ownerClient.GetAsync(
            $"/api/export-plans/{ownedPlanId}/chat/sessions/{created.Data.Id}/messages");
        var history = await historyResponse.Content
            .ReadFromJsonAsync<ApiResponse<ChatHistoryPageDto>>(JsonOptions);

        Assert.Equal(HttpStatusCode.OK, historyResponse.StatusCode);
        Assert.NotNull(history?.Data);
        Assert.Equal(2, history!.Data!.TotalCount);
        Assert.Collection(
            history.Data.Items,
            user =>
            {
                Assert.Equal(ChatMessageRole.User, user.Role);
                Assert.Equal(request.Content, user.Content);
            },
            assistant =>
            {
                Assert.Equal(ChatMessageRole.Assistant, assistant.Role);
                Assert.Equal("إجابة آمنة", assistant.Content);
                Assert.Equal(ChatMessageStatus.Completed, assistant.Status);
            });

        var crossTenantHistory = await otherOwnerClient.GetAsync(
            $"/api/export-plans/{ownedPlanId}/chat/sessions/{created.Data.Id}/messages");
        Assert.Equal(HttpStatusCode.NotFound, crossTenantHistory.StatusCode);
    }

    [Fact]
    public async Task FarmChat_CreatesOnlyReadOnlyFarmScopedSession()
    {
        var client = CreateOwnerClient(GreenmindApiFactory.FarmOwnerUserId);

        var response = await client.PostAsJsonAsync(
            "/api/farm/chat/sessions",
            new CreateChatSessionRequest { Title = "مقارنة خطط المزرعة" });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var created = await response.Content.ReadFromJsonAsync<ApiResponse<ChatSessionDto>>(JsonOptions);
        Assert.NotNull(created?.Data);
        Assert.Equal(ChatScope.Farm, created!.Data!.Scope);
        Assert.Null(created.Data.ExportPlanId);
    }

    private HttpClient CreateOwnerClient(string userId)
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Test-Role", RoleNames.FarmOwner);
        client.DefaultRequestHeaders.Add("X-Test-UserId", userId);
        return client;
    }

    private async Task<(int OwnedPlanId, int OtherPlanId)> AddPlansAsync()
    {
        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<GreenmindDbContext>();

        var farms = await dbContext.Farms
            .ToDictionaryAsync(farm => farm.OwnerUserId, farm => farm.Id);

        var suffix = Guid.NewGuid().ToString("N")[..8];
        var owned = BuildPlan(farms[GreenmindApiFactory.FarmOwnerUserId], $"Owned {suffix}");
        var other = BuildPlan(farms[GreenmindApiFactory.OtherFarmOwnerUserId], $"Other {suffix}");
        dbContext.ExportPlans.AddRange(owned, other);
        await dbContext.SaveChangesAsync();
        return (owned.Id, other.Id);
    }

    private static ExportPlan BuildPlan(int farmId, string name) => new()
    {
        FarmId = farmId,
        CropId = 1,
        DestinationId = 1,
        Name = name,
        Governorate = "القاهرة",
        City = "القاهرة",
        AreaInFeddan = 5,
        PlantingDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-30)),
        ExpectedHarvestDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(30)),
        ExpectedExportDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(40)),
        Status = ExportPlanStatus.Planning
    };

    private static System.Text.Json.JsonSerializerOptions CreateJsonOptions()
    {
        var options = new System.Text.Json.JsonSerializerOptions(
            System.Text.Json.JsonSerializerDefaults.Web);
        options.Converters.Add(new ArabicBusinessEnumJsonConverterFactory());
        return options;
    }
}
