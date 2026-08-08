using Greenmind.Application.Contracts.Planning;
using Greenmind.Application.Dtos.AI;
using Greenmind.Application.Dtos.Planning;
using Greenmind.Domain.Consts.Enums;
using Greenmind.Domain.Entities;
using Greenmind.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using DomainTaskStatus = Greenmind.Domain.Consts.Enums.TaskStatus;

namespace Greenmind.Infrastructure.Planning;

public sealed class ScheduleRevisionService(
    GreenmindDbContext db,
    IExportPlanPlanningService planner,
    TimeProvider timeProvider) : IScheduleRevisionService
{
    public async Task<ScheduleRevisionDto?> QueueAsync(int exportPlanId, string source, CancellationToken cancellationToken = default)
    {
        var planExists = await db.ExportPlans.AnyAsync(plan => plan.Id == exportPlanId, cancellationToken);
        if (!planExists) return null;

        var pending = await db.ScheduleRevisions
            .Where(revision => revision.ExportPlanId == exportPlanId && revision.Status == ScheduleRevisionStatus.Pending)
            .OrderByDescending(revision => revision.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);
        if (pending is not null)
        {
            pending.Source = source;
            await db.SaveChangesAsync(cancellationToken);
            return Map(pending);
        }

        var revision = new ScheduleRevision { ExportPlanId = exportPlanId, Source = source };
        db.ScheduleRevisions.Add(revision);
        await db.SaveChangesAsync(cancellationToken);
        return Map(revision);
    }

    public async Task<ScheduleRevisionDto?> GetLatestAsync(int exportPlanId, CancellationToken cancellationToken = default)
    {
        var revision = await db.ScheduleRevisions.AsNoTracking().Include(item => item.Items)
            .Where(item => item.ExportPlanId == exportPlanId).OrderByDescending(item => item.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);
        return revision is null ? null : Map(revision);
    }

    public async Task ProcessAsync(int revisionId, CancellationToken cancellationToken = default)
    {
        var revision = await db.ScheduleRevisions.Include(item => item.Items)
            .SingleOrDefaultAsync(item => item.Id == revisionId, cancellationToken);
        if (revision is null || revision.Status != ScheduleRevisionStatus.Pending) return;

        revision.Status = ScheduleRevisionStatus.InProgress;
        revision.StartedAtUtc = timeProvider.GetUtcNow().UtcDateTime;
        revision.AttemptCount++;
        await db.SaveChangesAsync(cancellationToken);
        try
        {
            var proposed = await planner.GenerateRevisionTasksAsync(revision.ExportPlanId, cancellationToken);
            if (proposed.Count == 0)
                throw new InvalidOperationException("The AI planner returned no safe schedule changes.");
            await ApplyAsync(revisionId, proposed, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception)
        {
            var failed = await db.ScheduleRevisions.SingleAsync(item => item.Id == revisionId, cancellationToken);
            failed.Status = ScheduleRevisionStatus.Failed;
            failed.CompletedAtUtc = timeProvider.GetUtcNow().UtcDateTime;
            failed.FailureMessage = "تعذّر تحديث الجدول بالذكاء الاصطناعي. تم حفظ تحديثك ويمكنك المحاولة مرة أخرى.";
            await db.SaveChangesAsync(cancellationToken);
        }
    }

    private async Task ApplyAsync(int revisionId, IReadOnlyList<TaskItemDto> proposed, CancellationToken cancellationToken)
    {
        var revision = await db.ScheduleRevisions.Include(item => item.Items).SingleAsync(item => item.Id == revisionId, cancellationToken);
        var today = DateOnly.FromDateTime(timeProvider.GetUtcNow().UtcDateTime);
        var types = await db.TaskTypes.ToDictionaryAsync(type => type.Name, StringComparer.OrdinalIgnoreCase, cancellationToken);
        var tasks = await db.FarmTasks.Include(task => task.Activities)
            .Where(task => task.ExportPlanId == revision.ExportPlanId).ToListAsync(cancellationToken);
        var mutable = tasks.Where(task =>
                (task.Origin == TaskOrigin.AIPlanned || task.Origin == TaskOrigin.AIReplanned) &&
                task.Status == DomainTaskStatus.Pending && task.PlannedDate >= today)
            .OrderBy(task => task.PlannedDate).ToList();
        var unmatched = new HashSet<int>(mutable.Select(task => task.Id));

        foreach (var candidate in proposed)
        {
            if (!types.TryGetValue(candidate.TaskTypeName, out var type)) continue;
            var match = mutable.Where(task => unmatched.Contains(task.Id) && task.TaskTypeId == type.Id)
                .OrderBy(task => Math.Abs(task.PlannedDate.DayNumber - candidate.PlannedDate.DayNumber)).FirstOrDefault();
            if (match is null)
            {
                var added = new FarmTask
                {
                    ExportPlanId = revision.ExportPlanId, TaskTypeId = type.Id, Title = candidate.Title,
                    Description = candidate.Description, Quantity = candidate.Quantity, Unit = candidate.Unit,
                    PlannedDate = candidate.PlannedDate, Status = DomainTaskStatus.Pending, Priority = candidate.Priority,
                    Origin = TaskOrigin.AIReplanned, Notes = candidate.Notes
                };
                added.Activities.Add(Activity("AIReplanAdded", null, added.Status, null, added.PlannedDate, candidate.Notes));
                db.FarmTasks.Add(added);
                revision.Items.Add(new ScheduleRevisionItem { FarmTask = added, Operation = ScheduleRevisionOperation.Added, TaskTitle = added.Title, NewPlannedDate = added.PlannedDate, Reason = "إضافة تلقائية بعد تحديث بيانات التنفيذ." });
                continue;
            }

            unmatched.Remove(match.Id);
            if (match.PlannedDate != candidate.PlannedDate)
            {
                var previousDate = match.PlannedDate;
                match.PlannedDate = candidate.PlannedDate;
                match.Title = candidate.Title;
                match.Description = candidate.Description;
                match.Quantity = candidate.Quantity;
                match.Unit = candidate.Unit;
                match.Priority = candidate.Priority;
                match.Origin = TaskOrigin.AIReplanned;
                match.Activities.Add(Activity("AIReplanned", match.Status, match.Status, previousDate, match.PlannedDate, candidate.Notes));
                revision.Items.Add(new ScheduleRevisionItem { FarmTaskId = match.Id, Operation = ScheduleRevisionOperation.Moved, TaskTitle = match.Title, PreviousPlannedDate = previousDate, NewPlannedDate = match.PlannedDate, Reason = "إعادة جدولة تلقائية مع الحفاظ على القيود التي حددها المستخدم." });
            }
        }

        foreach (var task in mutable.Where(task => unmatched.Contains(task.Id)))
        {
            task.Status = DomainTaskStatus.Cancelled;
            task.Activities.Add(Activity("AIReplanCancelled", DomainTaskStatus.Pending, DomainTaskStatus.Cancelled, task.PlannedDate, task.PlannedDate, "أزيلت من الجدول بعد مراجعة الذكاء الاصطناعي."));
            revision.Items.Add(new ScheduleRevisionItem { FarmTaskId = task.Id, Operation = ScheduleRevisionOperation.Cancelled, TaskTitle = task.Title, PreviousPlannedDate = task.PlannedDate, Reason = "لم تعد مطلوبة في الجدول المحدّث." });
        }

        revision.Status = ScheduleRevisionStatus.Succeeded;
        revision.CompletedAtUtc = timeProvider.GetUtcNow().UtcDateTime;
        revision.FailureMessage = null;
        await db.SaveChangesAsync(cancellationToken);
    }

    private static FarmTaskActivity Activity(string type, DomainTaskStatus? previousStatus, DomainTaskStatus? newStatus, DateOnly? previousDate, DateOnly? newDate, string? note) => new()
    {
        EventType = type, Source = "AIReplan", ActorName = "GreenMind AI", PreviousStatus = previousStatus,
        NewStatus = newStatus, PreviousPlannedDate = previousDate, NewPlannedDate = newDate, Note = note
    };

    private static ScheduleRevisionDto Map(ScheduleRevision revision) => new(
        revision.Id, revision.ExportPlanId, revision.Status.ToString(), revision.Source, revision.CreatedAt,
        revision.StartedAtUtc, revision.CompletedAtUtc, revision.AttemptCount, revision.FailureMessage,
        revision.Items.OrderBy(item => item.CreatedAt).Select(item => new ScheduleRevisionItemDto(item.Id, item.FarmTaskId,
            item.Operation.ToString(), item.TaskTitle, item.PreviousPlannedDate, item.NewPlannedDate, item.Reason)).ToList());
}
