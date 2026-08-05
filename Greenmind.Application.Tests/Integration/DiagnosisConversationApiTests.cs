using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Greenmind.Api.Json;
using Greenmind.Application.Contracts.Api;
using Greenmind.Application.Contracts.Disease;
using Greenmind.Application.Dtos.Disease;
using Greenmind.Application.Dtos.TaskProposals;
using Greenmind.Domain.Consts;
using Greenmind.Domain.Consts.Enums;
using Greenmind.Domain.Entities;
using Greenmind.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Greenmind.Application.Tests.Integration;

public sealed class DiagnosisConversationApiTests(GreenmindApiFactory factory)
    : IClassFixture<GreenmindApiFactory>
{
    private static readonly JsonSerializerOptions JsonOptions = CreateJsonOptions();

    [Fact]
    public async Task Conversation_DrivesReadiness_FinalizesAndCreatesSafeTreatmentProposal()
    {
        var planId = await SeedStrawberryPlanAsync();
        using var owner = CreateOwnerClient(GreenmindApiFactory.FarmOwnerUserId);
        using var otherOwner = CreateOwnerClient(GreenmindApiFactory.OtherFarmOwnerUserId);

        var create = await owner.PostAsync($"/api/export-plans/{planId}/diagnoses", null);
        Assert.Equal(HttpStatusCode.Created, create.StatusCode);
        var diagnosis = (await create.Content
            .ReadFromJsonAsync<ApiResponse<DiagnosisDetailDto>>(JsonOptions))?.Data;
        Assert.NotNull(diagnosis);
        Assert.Equal(DiseaseAnalysisStatus.Draft, diagnosis!.Summary.Status);
        Assert.False(diagnosis.Readiness.IsReady);

        var hiddenFromAnotherFarmer = await otherOwner.GetAsync(
            $"/api/export-plans/{planId}/diagnoses/{diagnosis.Summary.Id}");
        Assert.Equal(HttpStatusCode.NotFound, hiddenFromAnotherFarmer.StatusCode);

        const string idempotencyKey = "diagnosis-turn-0001";
        using var message = MessageContent(idempotencyKey, includeImage: true);
        var sent = await owner.PostAsync(
            $"/api/export-plans/{planId}/diagnoses/{diagnosis.Summary.Id}/messages",
            message);
        Assert.Equal(HttpStatusCode.OK, sent.StatusCode);
        var turn = (await sent.Content
            .ReadFromJsonAsync<ApiResponse<DiagnosisTurnDto>>(JsonOptions))?.Data;
        Assert.NotNull(turn);
        Assert.True(turn!.Readiness.IsReady);
        var image = Assert.Single(turn.UserMessage.Images);

        var download = await owner.GetAsync(
            $"/api/export-plans/{planId}/diagnoses/{diagnosis.Summary.Id}/images/{image.Id}");
        Assert.Equal(HttpStatusCode.OK, download.StatusCode);
        Assert.True(download.Headers.CacheControl?.Private);

        using var duplicate = MessageContent(idempotencyKey, includeImage: false);
        var duplicateResponse = await owner.PostAsync(
            $"/api/export-plans/{planId}/diagnoses/{diagnosis.Summary.Id}/messages",
            duplicate);
        var duplicateTurn = (await duplicateResponse.Content
            .ReadFromJsonAsync<ApiResponse<DiagnosisTurnDto>>(JsonOptions))?.Data;
        Assert.Equal(turn.UserMessage.Id, duplicateTurn?.UserMessage.Id);
        Assert.Equal(turn.AssistantMessage.Id, duplicateTurn?.AssistantMessage.Id);

        var finalize = await owner.PostAsJsonAsync(
            $"/api/export-plans/{planId}/diagnoses/{diagnosis.Summary.Id}/finalize",
            new { force = false });
        Assert.Equal(HttpStatusCode.Accepted, finalize.StatusCode);

        using var afterClose = MessageContent("diagnosis-turn-0002", includeImage: false);
        var writeAfterClose = await owner.PostAsync(
            $"/api/export-plans/{planId}/diagnoses/{diagnosis.Summary.Id}/messages",
            afterClose);
        Assert.Equal(HttpStatusCode.Conflict, writeAfterClose.StatusCode);

        using (var scope = factory.Services.CreateScope())
        {
            var processor = scope.ServiceProvider.GetRequiredService<IDiseaseReportService>();
            Assert.True(await processor.ProcessNextPendingAsync());
        }

        var resultResponse = await owner.GetAsync(
            $"/api/export-plans/{planId}/diagnoses/{diagnosis.Summary.Id}");
        var result = (await resultResponse.Content
            .ReadFromJsonAsync<ApiResponse<DiagnosisDetailDto>>(JsonOptions))?.Data;
        Assert.Equal(DiseaseAnalysisStatus.Completed, result?.Summary.Status);
        Assert.True(result?.Summary.KnowledgeVerified);
        Assert.NotEmpty(result?.Sources ?? []);

        var treatmentResponse = await owner.PostAsync(
            $"/api/export-plans/{planId}/diagnoses/{diagnosis.Summary.Id}/treatment-proposal",
            null);
        Assert.True(
            treatmentResponse.StatusCode == HttpStatusCode.OK,
            $"Treatment proposal failed with {(int)treatmentResponse.StatusCode}: " +
            await treatmentResponse.Content.ReadAsStringAsync());
        var treatment = (await treatmentResponse.Content
            .ReadFromJsonAsync<ApiResponse<TaskChangeProposalDto>>(JsonOptions))?.Data;
        Assert.NotNull(treatment);
        Assert.All(treatment!.Items, item => Assert.NotEqual(3, item.ProposedValues.TaskTypeId));
        Assert.All(treatment.Items, item => Assert.NotEqual(0, item.ProposedValues.TaskTypeId));
        Assert.True(treatment.Items.Count >= 3);
        Assert.All(treatment.Items, item =>
        {
            Assert.True(ContainsArabic(item.ProposedValues.Title));
            Assert.True(ContainsArabic(item.ProposedValues.Unit));
            Assert.True(ContainsArabic(item.Reason));
        });

        var treatmentItem = treatment.Items[0];
        var updateResponse = await owner.PutAsJsonAsync(
            $"/api/task-change-proposals/{treatment.Id}",
            new
            {
                items = treatment.Items.Select(item => new
                {
                    id = item.Id,
                    isIncluded = true,
                    title = item.Id == treatmentItem.Id ? "متابعة الإصابة بعد التشخيص" : item.ProposedValues.Title,
                    description = item.Id == treatmentItem.Id ? "تصوير النباتات المصابة ومراجعة تطور الأعراض." : item.ProposedValues.Description,
                    quantity = item.Id == treatmentItem.Id ? 2 : item.ProposedValues.Quantity,
                    unit = item.Id == treatmentItem.Id ? "زيارة" : item.ProposedValues.Unit,
                    plannedDate = item.ProposedValues.PlannedDate,
                    priority = item.ProposedValues.Priority
                }).ToArray()
            });
        Assert.Equal(HttpStatusCode.OK, updateResponse.StatusCode);
        var updated = (await updateResponse.Content
            .ReadFromJsonAsync<ApiResponse<TaskChangeProposalDto>>(JsonOptions))?.Data;
        Assert.Equal("متابعة الإصابة بعد التشخيص", updated!.Items.Single(item => item.Id == treatmentItem.Id).ProposedValues.Title);

        var applyResponse = await owner.PostAsync(
            $"/api/task-change-proposals/{treatment.Id}/apply", null);
        Assert.Equal(HttpStatusCode.OK, applyResponse.StatusCode);
        using var verifyScope = factory.Services.CreateScope();
        var verifyDb = verifyScope.ServiceProvider.GetRequiredService<GreenmindDbContext>();
        var appliedTask = await verifyDb.FarmTasks.SingleAsync(item =>
            item.ExportPlanId == planId && item.Title == "متابعة الإصابة بعد التشخيص");
        Assert.Equal(2, appliedTask.Quantity);
        Assert.Equal("زيارة", appliedTask.Unit);
    }

    [Fact]
    public async Task CancelledConversation_IsArchivedAndReadOnly()
    {
        var planId = await SeedStrawberryPlanAsync();
        using var owner = CreateOwnerClient(GreenmindApiFactory.FarmOwnerUserId);
        var create = await owner.PostAsync($"/api/export-plans/{planId}/diagnoses", null);
        var diagnosis = (await create.Content
            .ReadFromJsonAsync<ApiResponse<DiagnosisDetailDto>>(JsonOptions))?.Data;

        var cancel = await owner.PostAsync(
            $"/api/export-plans/{planId}/diagnoses/{diagnosis!.Summary.Id}/cancel",
            null);
        Assert.Equal(HttpStatusCode.OK, cancel.StatusCode);
        var cancelled = (await cancel.Content
            .ReadFromJsonAsync<ApiResponse<DiagnosisDetailDto>>(JsonOptions))?.Data;
        Assert.Equal(DiseaseAnalysisStatus.Cancelled, cancelled?.Summary.Status);

        using var message = MessageContent("cancelled-turn-0001", includeImage: false);
        var response = await owner.PostAsync(
            $"/api/export-plans/{planId}/diagnoses/{diagnosis.Summary.Id}/messages",
            message);
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task ImageOnlyMessage_IsAcceptedAndStored()
    {
        var planId = await SeedStrawberryPlanAsync();
        using var owner = CreateOwnerClient(GreenmindApiFactory.FarmOwnerUserId);
        var create = await owner.PostAsync($"/api/export-plans/{planId}/diagnoses", null);
        var diagnosis = (await create.Content
            .ReadFromJsonAsync<ApiResponse<DiagnosisDetailDto>>(JsonOptions))?.Data;

        using var content = new MultipartFormDataContent();
        content.Add(new StringContent("image-only-message-0001"), "IdempotencyKey");
        var image = new ByteArrayContent(Convert.FromBase64String(
            "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mNk+A8AAQUBAScY42YAAAAASUVORK5CYII="));
        image.Headers.ContentType = new MediaTypeHeaderValue("image/png");
        content.Add(image, "Images", "leaf.png");

        var response = await owner.PostAsync(
            $"/api/export-plans/{planId}/diagnoses/{diagnosis!.Summary.Id}/messages",
            content);
        var turn = (await response.Content
            .ReadFromJsonAsync<ApiResponse<DiagnosisTurnDto>>(JsonOptions))?.Data;

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(string.Empty, turn?.UserMessage.Content);
        Assert.Single(turn?.UserMessage.Images ?? []);
    }

    private async Task<int> SeedStrawberryPlanAsync()
    {
        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<GreenmindDbContext>();
        var farmId = await dbContext.Farms
            .Where(item => item.OwnerUserId == GreenmindApiFactory.FarmOwnerUserId)
            .Select(item => item.Id)
            .SingleAsync();
        var plan = new ExportPlan
        {
            FarmId = farmId,
            CropId = 1,
            DestinationId = 1,
            Name = $"Diagnosis conversation {Guid.NewGuid():N}",
            Governorate = "القاهرة",
            City = "القاهرة",
            AreaInFeddan = 5,
            PlantingDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-30)),
            ExpectedHarvestDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(30)),
            ExpectedExportDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(40)),
            Status = ExportPlanStatus.Planning
        };
        dbContext.ExportPlans.Add(plan);
        await dbContext.SaveChangesAsync();
        return plan.Id;
    }

    private HttpClient CreateOwnerClient(string userId)
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Test-Role", RoleNames.FarmOwner);
        client.DefaultRequestHeaders.Add("X-Test-UserId", userId);
        return client;
    }

    private static MultipartFormDataContent MessageContent(string key, bool includeImage)
    {
        var content = new MultipartFormDataContent();
        content.Add(new StringContent("ظهرت بقع على الأوراق والثمار منذ ثلاثة أيام."), "Content");
        content.Add(new StringContent(key), "IdempotencyKey");
        if (includeImage)
        {
            var image = new ByteArrayContent(Convert.FromBase64String(
                "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mNk+A8AAQUBAScY42YAAAAASUVORK5CYII="));
            image.Headers.ContentType = new MediaTypeHeaderValue("image/png");
            content.Add(image, "Images", "strawberry-leaf.png");
        }
        return content;
    }

    private static JsonSerializerOptions CreateJsonOptions()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        options.Converters.Add(new ArabicBusinessEnumJsonConverterFactory());
        options.Converters.Add(new JsonStringEnumConverter());
        return options;
    }

    private static bool ContainsArabic(string value) =>
        value.Any(character => character is >= '\u0600' and <= '\u06ff');
}
