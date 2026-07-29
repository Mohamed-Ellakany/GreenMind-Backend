using System.Text.Json;
using Greenmind.Application.Contracts.Api;
using Greenmind.Application.Contracts.TaskProposals;
using Greenmind.Application.Dtos.TaskProposals;
using Greenmind.Domain.Consts.Enums;
using Greenmind.Domain.Entities;
using Greenmind.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Greenmind.Infrastructure.TaskProposals;

public sealed class TaskChangeProposalService(
    GreenmindDbContext dbContext,
    TimeProvider timeProvider) : ITaskChangeProposalService
{
    private static readonly JsonSerializerOptions JsonOptions =
        new(JsonSerializerDefaults.Web)
        {
            PropertyNameCaseInsensitive = true
        };

    public async Task<TaskProposalResult<TaskChangeProposalDto>> GetAsync(
        int proposalId,
        string ownerUserId,
        CancellationToken cancellationToken = default)
    {
        var proposal = await LoadOwnedProposalAsync(
            proposalId, ownerUserId, tracking: false, cancellationToken);
        return proposal is null
            ? TaskProposalResult<TaskChangeProposalDto>.NotFound()
            : TaskProposalResult<TaskChangeProposalDto>.Success(Map(proposal));
    }

    public async Task<TaskProposalResult<TaskChangeProposalDto>> ApplyAsync(
        int proposalId,
        string ownerUserId,
        CancellationToken cancellationToken = default)
    {
        var proposal = await LoadOwnedProposalAsync(
            proposalId, ownerUserId, tracking: true, cancellationToken);
        if (proposal is null)
            return TaskProposalResult<TaskChangeProposalDto>.NotFound();

        var now = timeProvider.GetUtcNow().UtcDateTime;
        if (proposal.Status != TaskChangeProposalStatus.PendingReview)
        {
            return TaskProposalResult<TaskChangeProposalDto>.Invalid(
                ApiErrorCodes.Conflict,
                "لا يمكن تطبيق اقتراح تمت مراجعته بالفعل.");
        }

        if (proposal.ExpiresAtUtc <= now)
        {
            proposal.Status = TaskChangeProposalStatus.Expired;
            proposal.ReviewedAtUtc = now;
            await dbContext.SaveChangesAsync(cancellationToken);
            return TaskProposalResult<TaskChangeProposalDto>.Invalid(
                ApiErrorCodes.ProposalExpired,
                "انتهت صلاحية الاقتراح.");
        }

        var today = DateOnly.FromDateTime(now);
        var plan = proposal.ExportPlan;
        var taskTypes = await dbContext.TaskTypes
            .AsNoTracking()
            .Select(type => type.Id)
            .ToHashSetAsync(cancellationToken);

        var currentTasks = await dbContext.FarmTasks
            .Where(task => task.ExportPlanId == plan.Id)
            .ToListAsync(cancellationToken);
        var currentById = currentTasks.ToDictionary(task => task.Id);

        var affectedIds = proposal.Items
            .Where(item =>
                item.Operation == TaskChangeOperation.Modify &&
                item.FarmTaskId.HasValue)
            .Select(item => item.FarmTaskId!.Value)
            .ToHashSet();
        var resultingSlots = currentTasks
            .Where(task => !affectedIds.Contains(task.Id))
            .Select(task => (task.TaskTypeId, task.PlannedDate))
            .ToHashSet();

        var validated = new List<ValidatedOperation>(proposal.Items.Count);
        foreach (var item in proposal.Items.OrderBy(item => item.Id))
        {
            var proposed = DeserializeProposed(item.ProposedValuesJson);
            if (proposed is null ||
                !taskTypes.Contains(proposed.TaskTypeId) ||
                string.IsNullOrWhiteSpace(proposed.Title) ||
                proposed.Title.Length > 200 ||
                proposed.Description?.Length > 1000 ||
                proposed.PlannedDate <= today ||
                proposed.PlannedDate > plan.ExpectedExportDate)
            {
                return TaskProposalResult<TaskChangeProposalDto>.Invalid(
                    ApiErrorCodes.TaskNotEligible,
                    "يحتوي الاقتراح على بيانات مهمة غير صالحة.");
            }

            FarmTask? task = null;
            if (item.Operation != TaskChangeOperation.Add)
            {
                if (!item.FarmTaskId.HasValue ||
                    !currentById.TryGetValue(item.FarmTaskId.Value, out task) ||
                    task.Status != Greenmind.Domain.Consts.Enums.TaskStatus.Pending ||
                    task.PlannedDate <= today)
                {
                    return TaskProposalResult<TaskChangeProposalDto>.Invalid(
                        ApiErrorCodes.TaskNotEligible,
                        "المهمة لم تعد مؤهلة للتعديل أو الإلغاء.");
                }

                var original = DeserializeOriginal(item.OriginalValuesJson);
                if (original is null || !MatchesSnapshot(task, original))
                {
                    return TaskProposalResult<TaskChangeProposalDto>.Invalid(
                        ApiErrorCodes.ProposalStale,
                        "تغيرت المهمة منذ إنشاء الاقتراح. راجع اقتراحاً جديداً.");
                }
            }

            if (item.Operation != TaskChangeOperation.Cancel &&
                !resultingSlots.Add((proposed.TaskTypeId, proposed.PlannedDate)))
            {
                return TaskProposalResult<TaskChangeProposalDto>.Invalid(
                    ApiErrorCodes.Conflict,
                    "سينشئ الاقتراح مهمة مكررة في اليوم نفسه.");
            }

            validated.Add(new ValidatedOperation(item.Operation, task, proposed));
        }

        foreach (var operation in validated)
        {
            switch (operation.Operation)
            {
                case TaskChangeOperation.Add:
                    dbContext.FarmTasks.Add(new FarmTask
                    {
                        ExportPlanId = plan.Id,
                        TaskTypeId = operation.Proposed.TaskTypeId,
                        Title = operation.Proposed.Title.Trim(),
                        Description = Normalize(operation.Proposed.Description),
                        PlannedDate = operation.Proposed.PlannedDate,
                        Status = Greenmind.Domain.Consts.Enums.TaskStatus.Pending,
                        Priority = operation.Proposed.Priority
                    });
                    break;

                case TaskChangeOperation.Modify:
                    operation.Task!.TaskTypeId = operation.Proposed.TaskTypeId;
                    operation.Task.Title = operation.Proposed.Title.Trim();
                    operation.Task.Description = Normalize(operation.Proposed.Description);
                    operation.Task.PlannedDate = operation.Proposed.PlannedDate;
                    operation.Task.Priority = operation.Proposed.Priority;
                    break;

                case TaskChangeOperation.Cancel:
                    operation.Task!.Status = Greenmind.Domain.Consts.Enums.TaskStatus.Cancelled;
                    break;
            }
        }

        proposal.Status = TaskChangeProposalStatus.Applied;
        proposal.ReviewedAtUtc = now;
        proposal.ReviewedByUserId = ownerUserId;

        await dbContext.SaveChangesAsync(cancellationToken);
        return TaskProposalResult<TaskChangeProposalDto>.Success(Map(proposal));
    }

    public async Task<TaskProposalResult<TaskChangeProposalDto>> RejectAsync(
        int proposalId,
        string ownerUserId,
        CancellationToken cancellationToken = default)
    {
        var proposal = await LoadOwnedProposalAsync(
            proposalId, ownerUserId, tracking: true, cancellationToken);
        if (proposal is null)
            return TaskProposalResult<TaskChangeProposalDto>.NotFound();

        if (proposal.Status != TaskChangeProposalStatus.PendingReview)
        {
            return TaskProposalResult<TaskChangeProposalDto>.Invalid(
                ApiErrorCodes.Conflict,
                "لا يمكن رفض اقتراح تمت مراجعته بالفعل.");
        }

        var now = timeProvider.GetUtcNow().UtcDateTime;
        proposal.Status = proposal.ExpiresAtUtc <= now
            ? TaskChangeProposalStatus.Expired
            : TaskChangeProposalStatus.Rejected;
        proposal.ReviewedAtUtc = now;
        proposal.ReviewedByUserId = ownerUserId;
        await dbContext.SaveChangesAsync(cancellationToken);

        return TaskProposalResult<TaskChangeProposalDto>.Success(Map(proposal));
    }

    private async Task<TaskChangeProposal?> LoadOwnedProposalAsync(
        int proposalId,
        string ownerUserId,
        bool tracking,
        CancellationToken cancellationToken)
    {
        var query = dbContext.TaskChangeProposals
            .Include(proposal => proposal.ExportPlan)
            .Include(proposal => proposal.Items)
            .AsQueryable();
        if (!tracking)
            query = query.AsNoTracking();

        return await query.SingleOrDefaultAsync(
            proposal =>
                proposal.Id == proposalId &&
                proposal.ExportPlan.Farm.OwnerUserId == ownerUserId,
            cancellationToken);
    }

    private static TaskChangeProposalDto Map(TaskChangeProposal proposal) => new(
        proposal.Id,
        proposal.ExportPlanId,
        proposal.Status,
        proposal.ExpiresAtUtc,
        proposal.ReviewedAtUtc,
        proposal.SourceChatMessageId,
        proposal.SourceDiseaseReportId,
        proposal.Items
            .OrderBy(item => item.Id)
            .Select(item => new TaskChangeProposalItemDto(
                item.Id,
                item.Operation,
                item.FarmTaskId,
                DeserializeOriginal(item.OriginalValuesJson),
                DeserializeProposed(item.ProposedValuesJson)
                    ?? throw new InvalidOperationException(
                        $"Proposal item {item.Id} contains invalid proposed values."),
                item.Reason))
            .ToList());

    private static ProposedTaskValuesDto? DeserializeProposed(string? json) =>
        string.IsNullOrWhiteSpace(json)
            ? null
            : JsonSerializer.Deserialize<ProposedTaskValuesDto>(json, JsonOptions);

    private static TaskOriginalValuesDto? DeserializeOriginal(string? json) =>
        string.IsNullOrWhiteSpace(json)
            ? null
            : JsonSerializer.Deserialize<TaskOriginalValuesDto>(json, JsonOptions);

    private static bool MatchesSnapshot(FarmTask task, TaskOriginalValuesDto original) =>
        task.Id == original.FarmTaskId &&
        task.TaskTypeId == original.TaskTypeId &&
        task.Title == original.Title &&
        task.Description == original.Description &&
        task.PlannedDate == original.PlannedDate &&
        task.Status == original.Status &&
        task.Priority == original.Priority;

    private static string? Normalize(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private sealed record ValidatedOperation(
        TaskChangeOperation Operation,
        FarmTask? Task,
        ProposedTaskValuesDto Proposed);
}
