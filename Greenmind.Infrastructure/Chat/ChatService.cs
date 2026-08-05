using System.Runtime.CompilerServices;
using System.Text.Json;
using Greenmind.Application.Contracts.Chat;
using Greenmind.Application.Dtos.Chat;
using Greenmind.Application.Interfaces.AI;
using Greenmind.Application.Contracts.TaskProposals;
using Greenmind.Domain.Consts.Enums;
using Greenmind.Domain.Entities;
using Greenmind.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Greenmind.Infrastructure.Iti;

namespace Greenmind.Infrastructure.Chat;

public sealed class ChatService(
    GreenmindDbContext dbContext,
    IConversationAiService conversationAiService,
    IPlanTaskProposalGenerator proposalGenerator,
    ITaskChangeProposalService proposalService,
    TimeProvider timeProvider,
    ILogger<ChatService> logger) : IChatService
{
    private static readonly JsonSerializerOptions ContextJsonOptions =
        new(JsonSerializerDefaults.Web);

    public async Task<ChatOperationResult<ChatSessionDto>> CreatePlanSessionAsync(
        int exportPlanId,
        string ownerUserId,
        CreateChatSessionRequest request,
        CancellationToken cancellationToken = default)
    {
        var plan = await dbContext.ExportPlans
            .AsNoTracking()
            .Where(item => item.Id == exportPlanId && item.Farm.OwnerUserId == ownerUserId)
            .Select(item => new { item.Id, item.FarmId })
            .SingleOrDefaultAsync(cancellationToken);

        if (plan is null)
            return ChatOperationResult<ChatSessionDto>.NotFound();

        var session = new ChatSession
        {
            FarmId = plan.FarmId,
            ExportPlanId = plan.Id,
            Scope = ChatScope.ExportPlan,
            Title = request.Title.Trim()
        };

        dbContext.ChatSessions.Add(session);
        await dbContext.SaveChangesAsync(cancellationToken);
        return ChatOperationResult<ChatSessionDto>.Success(MapSession(session));
    }

    public async Task<IReadOnlyList<ChatSessionDto>> ListPlanSessionsAsync(
        int exportPlanId,
        string ownerUserId,
        CancellationToken cancellationToken = default) =>
        await dbContext.ChatSessions
            .AsNoTracking()
            .Where(session =>
                session.Scope == ChatScope.ExportPlan &&
                session.ExportPlanId == exportPlanId &&
                session.Farm.OwnerUserId == ownerUserId)
            .OrderByDescending(session => session.UpdatedAtUtc ?? session.CreatedAt)
            .ThenByDescending(session => session.Id)
            .Select(session => new ChatSessionDto(
                session.Id,
                session.FarmId,
                session.ExportPlanId,
                session.Scope,
                session.Title,
                session.IsActive,
                session.CreatedAt,
                session.UpdatedAtUtc))
            .ToListAsync(cancellationToken);

    public async Task<ChatOperationResult<ChatSessionDto>> CreateFarmSessionAsync(
        string ownerUserId,
        CreateChatSessionRequest request,
        CancellationToken cancellationToken = default)
    {
        var farmId = await dbContext.Farms
            .AsNoTracking()
            .Where(farm => farm.OwnerUserId == ownerUserId)
            .Select(farm => (int?)farm.Id)
            .SingleOrDefaultAsync(cancellationToken);

        if (farmId is null)
            return ChatOperationResult<ChatSessionDto>.NotFound();

        var session = new ChatSession
        {
            FarmId = farmId.Value,
            ExportPlanId = null,
            Scope = ChatScope.Farm,
            Title = request.Title.Trim()
        };

        dbContext.ChatSessions.Add(session);
        await dbContext.SaveChangesAsync(cancellationToken);
        return ChatOperationResult<ChatSessionDto>.Success(MapSession(session));
    }

    public async Task<IReadOnlyList<ChatSessionDto>> ListFarmSessionsAsync(
        string ownerUserId,
        CancellationToken cancellationToken = default) =>
        await dbContext.ChatSessions
            .AsNoTracking()
            .Where(session =>
                session.Scope == ChatScope.Farm &&
                session.ExportPlanId == null &&
                session.Farm.OwnerUserId == ownerUserId)
            .OrderByDescending(session => session.UpdatedAtUtc ?? session.CreatedAt)
            .ThenByDescending(session => session.Id)
            .Select(session => new ChatSessionDto(
                session.Id,
                session.FarmId,
                session.ExportPlanId,
                session.Scope,
                session.Title,
                session.IsActive,
                session.CreatedAt,
                session.UpdatedAtUtc))
            .ToListAsync(cancellationToken);

    public async Task<ChatOperationResult<ChatHistoryPageDto>> GetMessagesAsync(
        ChatScope scope,
        int? exportPlanId,
        int sessionId,
        string ownerUserId,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        if (!await IsOwnedSessionAsync(
                scope, exportPlanId, sessionId, ownerUserId, cancellationToken))
        {
            return ChatOperationResult<ChatHistoryPageDto>.NotFound();
        }

        pageNumber = Math.Max(1, pageNumber);
        pageSize = Math.Clamp(pageSize, 1, 100);

        var query = dbContext.ChatMessages
            .AsNoTracking()
            .Where(message => message.ChatSessionId == sessionId);

        var totalCount = await query.CountAsync(cancellationToken);
        var messages = await query
            .OrderBy(message => message.CreatedAt)
            .ThenBy(message => message.Id)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .Select(message => new ChatMessageDto(
                message.Id,
                message.Role,
                message.Content,
                message.Status,
                message.CreatedAt,
                message.Proposals.Select(proposal => (int?)proposal.Id).FirstOrDefault()))
            .ToListAsync(cancellationToken);

        return ChatOperationResult<ChatHistoryPageDto>.Success(
            new ChatHistoryPageDto(messages, pageNumber, pageSize, totalCount));
    }

    public async Task<ChatOperationResult<ChatStreamHandle>> StartReplyAsync(
        ChatScope scope,
        int? exportPlanId,
        int sessionId,
        string ownerUserId,
        SendChatMessageRequest request,
        CancellationToken cancellationToken = default)
    {
        var session = await dbContext.ChatSessions
            .SingleOrDefaultAsync(item =>
                item.Id == sessionId &&
                item.Scope == scope &&
                item.ExportPlanId == exportPlanId &&
                item.Farm.OwnerUserId == ownerUserId &&
                item.IsActive,
                cancellationToken);

        if (session is null)
            return ChatOperationResult<ChatStreamHandle>.NotFound();

        var normalizedKey = request.IdempotencyKey.Trim();
        var existingUserMessage = await dbContext.ChatMessages
            .AsNoTracking()
            .SingleOrDefaultAsync(message =>
                message.ChatSessionId == sessionId &&
                message.IdempotencyKey == normalizedKey,
                cancellationToken);

        if (existingUserMessage is not null)
        {
            var replyReference = BuildReplyReference(existingUserMessage.Id);
            var existingAssistantMessage = await dbContext.ChatMessages
                .AsNoTracking()
                .Where(message =>
                    message.ChatSessionId == sessionId &&
                    message.Role == ChatMessageRole.Assistant &&
                    message.Status == ChatMessageStatus.Completed &&
                    message.ContextReferencesJson == replyReference)
                .Select(message => new ChatMessageDto(
                    message.Id,
                    message.Role,
                    message.Content,
                    message.Status,
                    message.CreatedAt,
                    message.Proposals.Select(proposal => (int?)proposal.Id).FirstOrDefault()))
                .SingleOrDefaultAsync(cancellationToken);

            if (existingAssistantMessage is not null)
            {
                return ChatOperationResult<ChatStreamHandle>.Success(
                    new ChatStreamHandle(ReplayAsync(existingAssistantMessage)));
            }
        }

        ChatMessage userMessage;
        if (existingUserMessage is null)
        {
            userMessage = new ChatMessage
            {
                ChatSessionId = session.Id,
                Role = ChatMessageRole.User,
                Content = request.Content.Trim(),
                Status = ChatMessageStatus.Completed,
                IdempotencyKey = normalizedKey
            };
            dbContext.ChatMessages.Add(userMessage);
            session.UpdatedAtUtc = timeProvider.GetUtcNow().UtcDateTime;
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        else
        {
            userMessage = await dbContext.ChatMessages
                .SingleAsync(message => message.Id == existingUserMessage.Id, cancellationToken);
        }

        var recentMessages = await dbContext.ChatMessages
            .AsNoTracking()
            .Where(message =>
                message.ChatSessionId == session.Id &&
                message.Id < userMessage.Id &&
                message.Id > (session.SummaryThroughMessageId ?? 0) &&
                message.Status == ChatMessageStatus.Completed)
            .OrderByDescending(message => message.Id)
            .Take(20)
            .Select(message => new ConversationHistoryItem(message.Role, message.Content))
            .ToListAsync(cancellationToken);
        recentMessages.Reverse();

        var contextJson = scope == ChatScope.ExportPlan
            ? await BuildPlanContextAsync(exportPlanId!.Value, ownerUserId, cancellationToken)
            : await BuildFarmContextAsync(session.FarmId, ownerUserId, cancellationToken);

        var aiRequest = new ConversationAiRequest(
            scope,
            session.Title,
            userMessage.Content,
            contextJson,
            session.Summary,
            recentMessages,
            request.Images
                .Where(image => image.ContentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase))
                .Take(4)
                .Select(image => new ConversationImage(
                    image.ContentType[(image.ContentType.IndexOf('/') + 1)..],
                    image.DataBase64))
                .ToList());

        return ChatOperationResult<ChatStreamHandle>.Success(
            new ChatStreamHandle(StreamAndPersistAsync(
                session,
                userMessage.Id,
                aiRequest,
                cancellationToken)));
    }

    private async IAsyncEnumerable<ChatStreamEventDto> StreamAndPersistAsync(
        ChatSession session,
        int userMessageId,
        ConversationAiRequest aiRequest,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var content = new System.Text.StringBuilder();
        Exception? failure = null;
        await using var enumerator = conversationAiService
            .StreamReplyAsync(aiRequest, cancellationToken)
            .GetAsyncEnumerator(cancellationToken);

        while (true)
        {
            string? delta = null;
            var hasNext = false;
            try
            {
                hasNext = await enumerator.MoveNextAsync();
                if (hasNext)
                    delta = enumerator.Current;
            }
            catch (Exception exception)
            {
                failure = exception;
            }

            if (failure is not null || !hasNext)
                break;

            content.Append(delta);
            yield return new ChatStreamEventDto(
                "delta",
                ChatMessageStatus.Sending,
                Delta: delta);
        }

        var completedText = content.ToString().Trim();
        if (failure is null && completedText.Length == 0)
            failure = new InvalidOperationException("The AI provider returned an empty response.");

        ChatMessage? assistantMessage = null;
        if (failure is null)
        {
            try
            {
                assistantMessage = new ChatMessage
                {
                    ChatSessionId = session.Id,
                    Role = ChatMessageRole.Assistant,
                    Content = completedText,
                    Status = ChatMessageStatus.Completed,
                    ContextReferencesJson = BuildReplyReference(userMessageId)
                };

                dbContext.ChatMessages.Add(assistantMessage);
                session.UpdatedAtUtc = timeProvider.GetUtcNow().UtcDateTime;
                await dbContext.SaveChangesAsync(CancellationToken.None);
            }
            catch (Exception exception)
            {
                failure = exception;
            }
        }

        int? taskChangeProposalId = null;
        if (failure is null &&
            aiRequest.Scope == ChatScope.ExportPlan &&
            session.ExportPlanId.HasValue)
        {
            try
            {
                var generatedItems = await proposalGenerator.TryGenerateAsync(
                    aiRequest.UserMessage,
                    aiRequest.ContextJson,
                    CancellationToken.None);
                var proposalResult = await proposalService.CreateFromAiAsync(
                    session.ExportPlanId.Value,
                    assistantMessage!.Id,
                    generatedItems,
                    CancellationToken.None);
                taskChangeProposalId = proposalResult.Value?.Id;
            }
            catch (Exception exception)
            {
                // Proposal generation is optional; a safe chat answer remains valid.
                logger.LogWarning(
                    exception,
                    "Task-change proposal generation failed for chat session {ChatSessionId} and assistant message {AssistantMessageId}.",
                    session.Id,
                    assistantMessage!.Id);
            }
        }

        if (failure is null)
        {
            try
            {
                await UpdateConversationSummaryAsync(session, CancellationToken.None);
            }
            catch (Exception exception)
            {
                // Summarization is an optimization and must never invalidate a reply.
                logger.LogWarning(
                    exception,
                    "Conversation summarization failed for chat session {ChatSessionId}.",
                    session.Id);
            }
        }

        if (failure is null)
        {
            yield return new ChatStreamEventDto(
                "completed",
                ChatMessageStatus.Completed,
                MessageId: assistantMessage!.Id,
                Content: assistantMessage.Content,
                TaskChangeProposalId: taskChangeProposalId);
            yield break;
        }

        if (failure is OperationCanceledException)
        {
            logger.LogInformation(
                "AI chat request was cancelled for session {ChatSessionId}, scope {ChatScope}, user message {UserMessageId}.",
                session.Id,
                aiRequest.Scope,
                userMessageId);
        }
        else
        {
            logger.LogError(
                failure,
                "AI chat reply failed for session {ChatSessionId}, scope {ChatScope}, user message {UserMessageId}.",
                session.Id,
                aiRequest.Scope,
                userMessageId);
        }

        var failedMessage = await PersistFailedReplyAsync(session, userMessageId);
        if (failure is OperationCanceledException)
            throw failure;

        yield return new ChatStreamEventDto(
            "failed",
            ChatMessageStatus.Failed,
            MessageId: failedMessage.Id,
            Content: failedMessage.Content,
            Retryable: true);
    }

    private async Task<ChatMessage> PersistFailedReplyAsync(
        ChatSession session,
        int userMessageId)
    {
        var failedMessage = new ChatMessage
        {
            ChatSessionId = session.Id,
            Role = ChatMessageRole.Assistant,
            Content = "تعذر إكمال الرد الآن. يمكنك إعادة المحاولة بأمان.",
            Status = ChatMessageStatus.Failed,
            ContextReferencesJson = BuildReplyReference(userMessageId)
        };

        dbContext.ChatMessages.Add(failedMessage);
        session.UpdatedAtUtc = timeProvider.GetUtcNow().UtcDateTime;
        await dbContext.SaveChangesAsync(CancellationToken.None);
        return failedMessage;
    }

    private async Task UpdateConversationSummaryAsync(
        ChatSession session,
        CancellationToken cancellationToken)
    {
        const int retainRecentMessages = 12;
        const int summarizeAtMessageCount = 20;
        const int maximumSummaryLength = 6000;

        var unsummarized = await dbContext.ChatMessages
            .AsNoTracking()
            .Where(message =>
                message.ChatSessionId == session.Id &&
                message.Id > (session.SummaryThroughMessageId ?? 0) &&
                message.Status == ChatMessageStatus.Completed)
            .OrderBy(message => message.Id)
            .Select(message => new { message.Id, message.Role, message.Content })
            .ToListAsync(cancellationToken);

        if (unsummarized.Count < summarizeAtMessageCount)
            return;

        var summarizedMessages = unsummarized
            .Take(unsummarized.Count - retainRecentMessages)
            .ToList();
        if (summarizedMessages.Count == 0)
            return;

        var summaryBuilder = new System.Text.StringBuilder();
        if (!string.IsNullOrWhiteSpace(session.Summary))
            summaryBuilder.AppendLine(session.Summary.Trim());
        summaryBuilder.AppendLine("سجل المحادثة السابق:");
        foreach (var message in summarizedMessages)
        {
            summaryBuilder
                .Append(message.Role == ChatMessageRole.User ? "المستخدم: " : "المساعد: ")
                .AppendLine(message.Content.Trim());
        }

        var summary = summaryBuilder.ToString().Trim();
        session.Summary = summary.Length <= maximumSummaryLength
            ? summary
            : summary[^maximumSummaryLength..];
        session.SummaryThroughMessageId = summarizedMessages[^1].Id;
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task<bool> IsOwnedSessionAsync(
        ChatScope scope,
        int? exportPlanId,
        int sessionId,
        string ownerUserId,
        CancellationToken cancellationToken) =>
        await dbContext.ChatSessions.AsNoTracking().AnyAsync(
            session =>
                session.Id == sessionId &&
                session.Scope == scope &&
                session.ExportPlanId == exportPlanId &&
                session.Farm.OwnerUserId == ownerUserId,
            cancellationToken);

    private async Task<string> BuildPlanContextAsync(
        int exportPlanId,
        string ownerUserId,
        CancellationToken cancellationToken)
    {
        var plan = await dbContext.ExportPlans
            .AsNoTracking()
            .Where(item => item.Id == exportPlanId && item.Farm.OwnerUserId == ownerUserId)
            .Select(item => new
            {
                item.Id,
                item.Name,
                Crop = item.Crop.Name,
                Destination = item.Destination.Name,
                item.Governorate,
                item.City,
                item.AreaInFeddan,
                item.PlantingDate,
                item.ExpectedHarvestDate,
                item.ExpectedExportDate,
                Status = item.Status.ToString()
            })
            .SingleAsync(cancellationToken);

        var tasks = await dbContext.FarmTasks
            .AsNoTracking()
            .Where(task => task.ExportPlanId == exportPlanId)
            .OrderByDescending(task => task.PlannedDate)
            .Take(50)
            .Select(task => new
            {
                task.Id,
                Type = task.TaskType.Name,
                task.Title,
                task.PlannedDate,
                Status = task.Status.ToString(),
                Priority = task.Priority.ToString()
            })
            .ToListAsync(cancellationToken);

        var soil = await dbContext.SoilMeasurements
            .AsNoTracking()
            .Where(item => item.ExportPlanId == exportPlanId)
            .OrderByDescending(item => item.MeasurementDate)
            .Take(5)
            .Select(item => new
            {
                item.MeasurementDate,
                item.PH,
                item.EC,
                item.Nitrogen,
                item.Phosphorus,
                item.Potassium,
                item.Moisture
            })
            .ToListAsync(cancellationToken);

        var diseases = await dbContext.DiseaseReports
            .AsNoTracking()
            .Where(report => report.ExportPlanId == exportPlanId)
            .OrderByDescending(report => report.ObservationDate)
            .Take(10)
            .Select(report => new
            {
                report.Id,
                report.ObservationDate,
                Status = report.Status.ToString(),
                report.SuspectedDisease,
                report.ConfidencePercentage,
                Severity = report.Severity.HasValue ? report.Severity.Value.ToString() : null
            })
            .ToListAsync(cancellationToken);

        var plannedBudget = await dbContext.ExportPlanBudgets
            .AsNoTracking()
            .Where(budget => budget.ExportPlanId == exportPlanId)
            .Select(budget => budget.TotalBudget ??
                (decimal?)budget.Lines.Sum(line => line.PlannedAmount))
            .SingleOrDefaultAsync(cancellationToken);

        var actualExpenses = await dbContext.Expenses
            .AsNoTracking()
            .Where(expense => expense.ExportPlanId == exportPlanId)
            .SumAsync(expense => (decimal?)expense.Amount, cancellationToken) ?? 0m;

        return JsonSerializer.Serialize(
            new
            {
                plan,
                tasks,
                soil,
                diseases,
                costs = new { plannedBudget, actualExpenses }
            },
            ContextJsonOptions);
    }

    private async Task<string> BuildFarmContextAsync(
        int farmId,
        string ownerUserId,
        CancellationToken cancellationToken)
    {
        var plans = await dbContext.ExportPlans
            .AsNoTracking()
            .Where(plan => plan.FarmId == farmId && plan.Farm.OwnerUserId == ownerUserId)
            .OrderBy(plan => plan.Id)
            .Select(plan => new
            {
                plan.Id,
                plan.Name,
                Crop = plan.Crop.Name,
                Destination = plan.Destination.Name,
                Status = plan.Status.ToString(),
                plan.ExpectedHarvestDate,
                plan.ExpectedExportDate,
                TaskCount = plan.FarmTasks.Count,
                PendingTaskCount = plan.FarmTasks.Count(task =>
                    task.Status == Greenmind.Domain.Consts.Enums.TaskStatus.Pending),
                DiseaseReportCount = plan.DiseaseReports.Count,
                ActualExpenses = plan.Expenses.Sum(expense => (decimal?)expense.Amount) ?? 0m
            })
            .ToListAsync(cancellationToken);

        var orderedPlans = plans.Select((plan, index) => new
        {
            Ordinal = index + 1,
            plan.Id,
            plan.Name,
            plan.Crop,
            plan.Destination,
            plan.Status,
            plan.ExpectedHarvestDate,
            plan.ExpectedExportDate,
            plan.TaskCount,
            plan.PendingTaskCount,
            plan.DiseaseReportCount,
            plan.ActualExpenses
        });

        return JsonSerializer.Serialize(new { farmId, plans = orderedPlans }, ContextJsonOptions);
    }

    private static ChatSessionDto MapSession(ChatSession session) => new(
        session.Id,
        session.FarmId,
        session.ExportPlanId,
        session.Scope,
        session.Title,
        session.IsActive,
        session.CreatedAt,
        session.UpdatedAtUtc);

    private static string BuildReplyReference(int userMessageId) =>
        $"{{\"replyToMessageId\":{userMessageId}}}";

    private static async IAsyncEnumerable<ChatStreamEventDto> ReplayAsync(
        ChatMessageDto message)
    {
        yield return new ChatStreamEventDto(
            "completed",
            ChatMessageStatus.Completed,
            MessageId: message.Id,
            Content: message.Content,
            TaskChangeProposalId: message.TaskChangeProposalId);
        await Task.CompletedTask;
    }
}
