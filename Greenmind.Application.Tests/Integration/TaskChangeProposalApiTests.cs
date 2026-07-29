using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Greenmind.Api.Json;
using Greenmind.Application.Contracts.Api;
using Greenmind.Application.Contracts.TaskProposals;
using Greenmind.Application.Dtos.TaskProposals;
using Greenmind.Application.Interfaces.AI;
using Greenmind.Domain.Consts;
using Greenmind.Domain.Consts.Enums;
using Greenmind.Domain.Entities;
using Greenmind.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TaskStatus = Greenmind.Domain.Consts.Enums.TaskStatus;

namespace Greenmind.Application.Tests.Integration;

public sealed class TaskChangeProposalApiTests(GreenmindApiFactory factory)
    : IClassFixture<GreenmindApiFactory>
{
    private static readonly JsonSerializerOptions ApiJsonOptions = CreateApiJsonOptions();
    private static readonly JsonSerializerOptions SnapshotJsonOptions =
        new(JsonSerializerDefaults.Web);

    [Fact]
    public async Task CreateFromAi_CreatesReviewableProposal_WithoutMutatingTasks()
    {
        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<GreenmindDbContext>();
        var proposalService = scope.ServiceProvider
            .GetRequiredService<ITaskChangeProposalService>();
        var farmId = await dbContext.Farms
            .Where(farm => farm.OwnerUserId == GreenmindApiFactory.FarmOwnerUserId)
            .Select(farm => farm.Id)
            .SingleAsync();
        var taskTypeId = await dbContext.TaskTypes
            .OrderBy(type => type.Id)
            .Select(type => type.Id)
            .FirstAsync();
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var plan = new ExportPlan
        {
            FarmId = farmId,
            CropId = 1,
            DestinationId = 1,
            Name = $"AI proposal plan {suffix}",
            Governorate = "القاهرة",
            City = "القاهرة",
            AreaInFeddan = 4,
            PlantingDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-20)),
            ExpectedHarvestDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(30)),
            ExpectedExportDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(45)),
            Status = ExportPlanStatus.Planning
        };
        var session = new ChatSession
        {
            FarmId = farmId,
            ExportPlan = plan,
            Scope = ChatScope.ExportPlan,
            Title = "AI proposal test"
        };
        var assistantMessage = new ChatMessage
        {
            ChatSession = session,
            Role = ChatMessageRole.Assistant,
            Content = "سأقترح مهمة جديدة لمراجعتك.",
            Status = ChatMessageStatus.Completed
        };
        dbContext.AddRange(plan, session, assistantMessage);
        await dbContext.SaveChangesAsync();
        var tasksBefore = await dbContext.FarmTasks.CountAsync(task =>
            task.ExportPlanId == plan.Id);

        var result = await proposalService.CreateFromAiAsync(
            plan.Id,
            assistantMessage.Id,
            [
                new GeneratedTaskChangeItem(
                    "add",
                    null,
                    new ProposedTaskValuesDto(
                        taskTypeId,
                        "فحص جاهزية المحصول",
                        "مراجعة حالة المحصول قبل التجهيز.",
                        DateOnly.FromDateTime(DateTime.UtcNow.AddDays(5)),
                        TaskPriority.High),
                    "طلب المستخدم إضافة فحص قبل التجهيز")
            ]);

        Assert.Equal(TaskProposalResultStatus.Succeeded, result.Status);
        Assert.NotNull(result.Value);
        Assert.Equal(TaskChangeProposalStatus.PendingReview, result.Value!.Status);
        Assert.Equal(assistantMessage.Id, result.Value.SourceChatMessageId);
        Assert.Equal(TaskChangeOperation.Add, Assert.Single(result.Value.Items).Operation);
        Assert.Equal(tasksBefore, await dbContext.FarmTasks.CountAsync(task =>
            task.ExportPlanId == plan.Id));

        var ownerClient = CreateOwnerClient(GreenmindApiFactory.FarmOwnerUserId);
        var response = await ownerClient.GetAsync(
            $"/api/task-change-proposals/{result.Value.Id}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Apply_ModifiesEligibleTask_AndIsTenantIsolated()
    {
        var seeded = await SeedModifyProposalAsync(staleSnapshot: false);
        var otherOwnerClient = CreateOwnerClient(GreenmindApiFactory.OtherFarmOwnerUserId);

        var hiddenResponse = await otherOwnerClient.GetAsync(
            $"/api/task-change-proposals/{seeded.ProposalId}");
        Assert.Equal(HttpStatusCode.NotFound, hiddenResponse.StatusCode);

        var ownerClient = CreateOwnerClient(GreenmindApiFactory.FarmOwnerUserId);
        var response = await ownerClient.PostAsync(
            $"/api/task-change-proposals/{seeded.ProposalId}/apply",
            content: null);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content
            .ReadFromJsonAsync<ApiResponse<TaskChangeProposalDto>>(ApiJsonOptions);
        Assert.NotNull(body?.Data);
        Assert.Equal(TaskChangeProposalStatus.Applied, body!.Data!.Status);

        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<GreenmindDbContext>();
        var task = await dbContext.FarmTasks.SingleAsync(item => item.Id == seeded.TaskId);
        Assert.Equal(seeded.NewTitle, task.Title);
        Assert.Equal(seeded.NewDate, task.PlannedDate);
        Assert.Equal(TaskStatus.Pending, task.Status);
    }

    [Fact]
    public async Task Apply_RejectsStaleSnapshot_WithoutMutatingTask()
    {
        var seeded = await SeedModifyProposalAsync(staleSnapshot: true);
        var ownerClient = CreateOwnerClient(GreenmindApiFactory.FarmOwnerUserId);

        var response = await ownerClient.PostAsync(
            $"/api/task-change-proposals/{seeded.ProposalId}/apply",
            content: null);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        var body = await response.Content
            .ReadFromJsonAsync<ApiResponse<TaskChangeProposalDto>>(ApiJsonOptions);
        Assert.Equal(ApiErrorCodes.ProposalStale, body?.Error?.Code);

        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<GreenmindDbContext>();
        var task = await dbContext.FarmTasks.SingleAsync(item => item.Id == seeded.TaskId);
        var proposal = await dbContext.TaskChangeProposals
            .SingleAsync(item => item.Id == seeded.ProposalId);
        Assert.Equal(seeded.OriginalTitle, task.Title);
        Assert.Equal(TaskChangeProposalStatus.PendingReview, proposal.Status);
    }

    [Fact]
    public async Task Reject_MarksProposalRejected_WithoutChangingTask()
    {
        var seeded = await SeedModifyProposalAsync(staleSnapshot: false);
        var ownerClient = CreateOwnerClient(GreenmindApiFactory.FarmOwnerUserId);

        var response = await ownerClient.PostAsync(
            $"/api/task-change-proposals/{seeded.ProposalId}/reject",
            content: null);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content
            .ReadFromJsonAsync<ApiResponse<TaskChangeProposalDto>>(ApiJsonOptions);
        Assert.Equal(TaskChangeProposalStatus.Rejected, body?.Data?.Status);

        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<GreenmindDbContext>();
        var task = await dbContext.FarmTasks.SingleAsync(item => item.Id == seeded.TaskId);
        Assert.Equal(seeded.OriginalTitle, task.Title);
    }

    private async Task<SeededProposal> SeedModifyProposalAsync(bool staleSnapshot)
    {
        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<GreenmindDbContext>();
        var farmId = await dbContext.Farms
            .Where(farm => farm.OwnerUserId == GreenmindApiFactory.FarmOwnerUserId)
            .Select(farm => farm.Id)
            .SingleAsync();
        var taskTypeId = await dbContext.TaskTypes
            .OrderBy(type => type.Id)
            .Select(type => type.Id)
            .FirstAsync();

        var suffix = Guid.NewGuid().ToString("N")[..8];
        var originalTitle = $"Original {suffix}";
        var newTitle = $"Updated {suffix}";
        var originalDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(10));
        var newDate = originalDate.AddDays(2);
        var plan = new ExportPlan
        {
            FarmId = farmId,
            CropId = 1,
            DestinationId = 1,
            Name = $"Proposal plan {suffix}",
            Governorate = "القاهرة",
            City = "القاهرة",
            AreaInFeddan = 5,
            PlantingDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-30)),
            ExpectedHarvestDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(30)),
            ExpectedExportDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(40)),
            Status = ExportPlanStatus.Planning
        };
        var task = new FarmTask
        {
            ExportPlan = plan,
            TaskTypeId = taskTypeId,
            Title = originalTitle,
            Description = "Original description",
            PlannedDate = originalDate,
            Status = TaskStatus.Pending,
            Priority = TaskPriority.Medium
        };
        dbContext.AddRange(plan, task);
        await dbContext.SaveChangesAsync();

        var snapshot = new TaskOriginalValuesDto(
            task.Id,
            taskTypeId,
            staleSnapshot ? $"{originalTitle} stale" : originalTitle,
            task.Description,
            originalDate,
            TaskStatus.Pending,
            TaskPriority.Medium);
        var proposed = new ProposedTaskValuesDto(
            taskTypeId,
            newTitle,
            "Updated description",
            newDate,
            TaskPriority.High);
        var proposal = new TaskChangeProposal
        {
            ExportPlanId = plan.Id,
            ExpiresAtUtc = DateTime.UtcNow.AddHours(1),
            Items =
            [
                new TaskChangeProposalItem
                {
                    Operation = TaskChangeOperation.Modify,
                    FarmTaskId = task.Id,
                    OriginalValuesJson = JsonSerializer.Serialize(
                        snapshot,
                        SnapshotJsonOptions),
                    ProposedValuesJson = JsonSerializer.Serialize(
                        proposed,
                        SnapshotJsonOptions),
                    Reason = "تحديث موعد المهمة"
                }
            ]
        };
        dbContext.TaskChangeProposals.Add(proposal);
        await dbContext.SaveChangesAsync();

        return new SeededProposal(
            proposal.Id,
            task.Id,
            originalTitle,
            newTitle,
            newDate);
    }

    private HttpClient CreateOwnerClient(string userId)
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Test-Role", RoleNames.FarmOwner);
        client.DefaultRequestHeaders.Add("X-Test-UserId", userId);
        return client;
    }

    private static JsonSerializerOptions CreateApiJsonOptions()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        options.Converters.Add(new ArabicBusinessEnumJsonConverterFactory());
        options.Converters.Add(new JsonStringEnumConverter());
        return options;
    }

    private sealed record SeededProposal(
        int ProposalId,
        int TaskId,
        string OriginalTitle,
        string NewTitle,
        DateOnly NewDate);
}
