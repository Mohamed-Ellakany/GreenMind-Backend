using System.Security.Cryptography;
using System.Text.Json;
using Greenmind.Application.Contracts.Api;
using Greenmind.Application.Contracts.Disease;
using Greenmind.Application.Contracts.ExportPlans;
using Greenmind.Application.Contracts.Storage;
using Greenmind.Application.Contracts.TaskProposals;
using Greenmind.Application.Dtos.Disease;
using Greenmind.Application.Dtos.TaskProposals;
using Greenmind.Application.Interfaces.AI;
using Greenmind.Domain.Consts;
using Greenmind.Domain.Consts.Enums;
using Greenmind.Domain.Entities;
using Greenmind.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Greenmind.Infrastructure.Disease;

public sealed class DiagnosisSessionService(
    GreenmindDbContext dbContext,
    IPrivateFileStorage fileStorage,
    IDiagnosisConversationAiService conversationAi,
    IPlanTaskProposalGenerator proposalGenerator,
    ITaskChangeProposalService proposalService,
    IClimateDataClient climateDataClient,
    TimeProvider timeProvider,
    ILogger<DiagnosisSessionService> logger) : IDiagnosisSessionService
{
    private const int MaximumImagesPerMessage = 5;
    private const int MaximumImagesPerSession = 10;
    private const int MaximumImageBytes = 10 * 1024 * 1024;
    private const int MaximumRequestBytes = 30 * 1024 * 1024;
    private const long MaximumSessionBytes = 60L * 1024 * 1024;
    private const int MinimumTreatmentPlanItems = 3;
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<DiseaseOperationResult<DiagnosisDetailDto>> CreateAsync(
        int exportPlanId,
        string ownerUserId,
        CancellationToken cancellationToken = default)
    {
        var plan = await dbContext.ExportPlans
            .AsNoTracking()
            .Where(item => item.Id == exportPlanId && item.Farm.OwnerUserId == ownerUserId)
            .Select(item => new { item.Id, item.FarmId, item.CropId })
            .SingleOrDefaultAsync(cancellationToken);
        if (plan is null)
            return DiseaseOperationResult<DiagnosisDetailDto>.NotFound();
        if (!CropCatalog.SupportsSmartDiagnosis(plan.CropId))
        {
            return DiseaseOperationResult<DiagnosisDetailDto>.Invalid(
                ApiErrorCodes.Conflict,
                "جلسات التشخيص الذكية متاحة للفراولة فقط في النسخة الحالية.");
        }

        var now = timeProvider.GetUtcNow().UtcDateTime;
        var facts = EmptyFacts();
        var readiness = BuildReadiness(facts, 0);
        var session = new ChatSession
        {
            FarmId = plan.FarmId,
            ExportPlanId = plan.Id,
            Scope = ChatScope.Diagnosis,
            Title = $"تشخيص جديد - {now:yyyy-MM-dd HH:mm}",
            UpdatedAtUtc = now
        };
        var report = new DiseaseReport
        {
            ExportPlanId = plan.Id,
            ChatSession = session,
            ObservationDate = DateOnly.FromDateTime(now),
            Status = DiseaseAnalysisStatus.Draft,
            CaseFactsJson = JsonSerializer.Serialize(facts, JsonOptions),
            ReadinessJson = JsonSerializer.Serialize(readiness, JsonOptions)
        };
        session.Messages.Add(new ChatMessage
        {
            Role = ChatMessageRole.Assistant,
            Status = ChatMessageStatus.Completed,
            Content = "أهلًا، هساعدك نجمع معلومات الحالة خطوة بخطوة. ارفع صورة واضحة للنبات المصاب، وقولي أي جزء متأثر ومتى بدأت الأعراض."
        });

        dbContext.DiseaseReports.Add(report);
        await dbContext.SaveChangesAsync(cancellationToken);
        return DiseaseOperationResult<DiagnosisDetailDto>.Success(
            await LoadDetailAsync(report.Id, ownerUserId, cancellationToken)
            ?? throw new InvalidOperationException("Created diagnosis could not be loaded."));
    }

    public async Task<DiseaseOperationResult<IReadOnlyList<DiagnosisSummaryDto>>> ListAsync(
        int exportPlanId,
        string ownerUserId,
        CancellationToken cancellationToken = default)
    {
        if (!await OwnsPlanAsync(exportPlanId, ownerUserId, cancellationToken))
            return DiseaseOperationResult<IReadOnlyList<DiagnosisSummaryDto>>.NotFound();

        var reports = await dbContext.DiseaseReports
            .AsNoTracking()
            .Include(item => item.ChatSession)
            .Include(item => item.Images)
            .Where(item => item.ExportPlanId == exportPlanId)
            .OrderByDescending(item => item.CreatedAt)
            .ToListAsync(cancellationToken);
        return DiseaseOperationResult<IReadOnlyList<DiagnosisSummaryDto>>.Success(
            reports.Select(MapSummary).ToList());
    }

    public async Task<DiseaseOperationResult<DiagnosisDetailDto>> GetAsync(
        int exportPlanId,
        int diagnosisId,
        string ownerUserId,
        CancellationToken cancellationToken = default)
    {
        var detail = await LoadDetailAsync(diagnosisId, ownerUserId, cancellationToken, exportPlanId);
        return detail is null
            ? DiseaseOperationResult<DiagnosisDetailDto>.NotFound()
            : DiseaseOperationResult<DiagnosisDetailDto>.Success(detail);
    }

    public async Task<DiseaseOperationResult<DiagnosisTurnDto>> SendMessageAsync(
        int exportPlanId,
        int diagnosisId,
        string ownerUserId,
        CreateDiagnosisMessageRequest request,
        CancellationToken cancellationToken = default)
    {
        var report = await LoadOwnedReportAsync(exportPlanId, diagnosisId, ownerUserId, true, cancellationToken);
        if (report?.ChatSession is null)
            return DiseaseOperationResult<DiagnosisTurnDto>.NotFound();
        if (report.Status is not (DiseaseAnalysisStatus.Draft or DiseaseAnalysisStatus.Ready) ||
            !report.ChatSession.IsActive)
        {
            return DiseaseOperationResult<DiagnosisTurnDto>.Conflict(
                ApiErrorCodes.Conflict,
                "جلسة التشخيص مغلقة ولا تقبل رسائل جديدة.");
        }

        var content = request.Content.Trim();
        if ((content.Length == 0 && request.Images.Count == 0) || content.Length > 4000)
            return InvalidTurn("اكتب رسالة أو أرفق صورة صالحة.");
        if (request.IdempotencyKey.Trim().Length is < 8 or > 100)
            return InvalidTurn("مفتاح الطلب غير صالح.");

        var existing = report.ChatSession.Messages
            .FirstOrDefault(message => message.IdempotencyKey == request.IdempotencyKey.Trim());
        if (existing is not null)
        {
            var assistant = report.ChatSession.Messages
                .FirstOrDefault(message => message.ContextReferencesJson == ReplyReference(existing.Id));
            if (assistant is not null)
            {
                return DiseaseOperationResult<DiagnosisTurnDto>.Success(new DiagnosisTurnDto(
                    MapMessage(existing, report.Images),
                    MapMessage(assistant, report.Images),
                    ReadReadiness(report),
                    ReadFacts(report)));
            }
        }

        var validated = ValidateImages(request.Images);
        if (validated.Error is not null)
            return InvalidTurn(validated.Error);
        if (report.Images.Count + validated.Images.Count > MaximumImagesPerSession ||
            report.Images.Sum(image => image.FileSize) + validated.Images.Sum(image => (long)image.Content.Length) > MaximumSessionBytes)
        {
            return InvalidTurn("الحد الأقصى للجلسة 10 صور وبإجمالي 60 ميجابايت.");
        }

        var storedKeys = new List<string>();
        try
        {
            var userMessage = new ChatMessage
            {
                ChatSessionId = report.ChatSession.Id,
                Role = ChatMessageRole.User,
                Status = ChatMessageStatus.Completed,
                Content = content,
                IdempotencyKey = request.IdempotencyKey.Trim()
            };
            dbContext.ChatMessages.Add(userMessage);

            foreach (var image in validated.Images)
            {
                await using var stream = new MemoryStream(image.Content, writable: false);
                var key = await fileStorage.SaveAsync("disease", image.Extension, stream, cancellationToken);
                storedKeys.Add(key);
                report.Images.Add(new DiseaseImage
                {
                    ChatMessage = userMessage,
                    OriginalFileName = image.OriginalName,
                    StorageKey = key,
                    ContentType = image.ContentType,
                    FileSize = image.Content.Length,
                    Checksum = Convert.ToHexString(SHA256.HashData(image.Content))
                });
            }

            report.ChatSession.UpdatedAtUtc = timeProvider.GetUtcNow().UtcDateTime;
            await dbContext.SaveChangesAsync(cancellationToken);

            var facts = ReadFacts(report);
            DiagnosisConversationTurn turn;
            ChatMessage assistantMessage;
            try
            {
                var recent = report.ChatSession.Messages
                    .Where(message => message.Id != userMessage.Id && message.Status == ChatMessageStatus.Completed)
                    .OrderBy(message => message.Id)
                    .Select(message => new ConversationHistoryItem(message.Role, message.Content))
                    .ToList();
                facts = DiagnosisConversationState.RecoverFacts(facts, recent, content);
                var planContext = await BuildPlanContextAsync(exportPlanId, ownerUserId, cancellationToken);
                turn = await conversationAi.ReplyAsync(new DiagnosisConversationRequest(
                    planContext,
                    content,
                    facts,
                    recent,
                    validated.Images.Select(image => new ConversationImage(
                        image.ContentType == "image/png" ? "png" : "jpeg",
                        Convert.ToBase64String(image.Content))).ToList(),
                    report.Images.Count,
                    ownerUserId,
                    exportPlanId), cancellationToken);
                facts = turn.Facts;
                var readinessAfterTurn = BuildReadiness(facts, report.Images.Count);
                assistantMessage = new ChatMessage
                {
                    ChatSessionId = report.ChatSession.Id,
                    Role = ChatMessageRole.Assistant,
                    Status = ChatMessageStatus.Completed,
                    Content = DiagnosisConversationState.BuildAssistantReply(
                        turn.AssistantText, readinessAfterTurn, validated.Images.Count > 0),
                    ContextReferencesJson = ReplyReference(userMessage.Id)
                };
            }
            catch (Exception exception)
            {
                logger.LogError(
                    exception,
                    "Diagnosis conversation AI call failed for session {SessionId}, report {ReportId}.",
                    report.ChatSession!.Id,
                    report.Id);
                var recent = report.ChatSession.Messages
                    .Where(message => message.Id != userMessage.Id && message.Status == ChatMessageStatus.Completed)
                    .OrderBy(message => message.Id)
                    .Select(message => new ConversationHistoryItem(message.Role, message.Content))
                    .ToList();
                facts = DiagnosisConversationState.RecoverFacts(facts, recent, content);
                assistantMessage = new ChatMessage
                {
                    ChatSessionId = report.ChatSession.Id,
                    Role = ChatMessageRole.Assistant,
                    Status = ChatMessageStatus.Failed,
                    Content = "تم حفظ رسالتك وصورك، لكن تعذر تحليل النواقص الآن. يمكنك إعادة إرسال رسالتك لاحقًا.",
                    ContextReferencesJson = ReplyReference(userMessage.Id)
                };
            }

            dbContext.ChatMessages.Add(assistantMessage);
            var readiness = BuildReadiness(facts, report.Images.Count);
            report.CaseFactsJson = JsonSerializer.Serialize(facts, JsonOptions);
            report.ReadinessJson = JsonSerializer.Serialize(readiness, JsonOptions);
            report.AffectedArea = facts.AffectedPart;
            report.UserNotes = JsonSerializer.Serialize(facts, JsonOptions);
            report.Status = readiness.IsReady ? DiseaseAnalysisStatus.Ready : DiseaseAnalysisStatus.Draft;
            await dbContext.SaveChangesAsync(cancellationToken);

            return DiseaseOperationResult<DiagnosisTurnDto>.Success(new DiagnosisTurnDto(
                MapMessage(userMessage, report.Images),
                MapMessage(assistantMessage, report.Images),
                readiness,
                facts));
        }
        catch
        {
            foreach (var key in storedKeys)
            {
                try { await fileStorage.DeleteAsync(key, CancellationToken.None); }
                catch { }
            }
            throw;
        }
    }

    public async Task<DiseaseOperationResult<DiagnosisDetailDto>> FinalizeAsync(
        int exportPlanId,
        int diagnosisId,
        string ownerUserId,
        bool force,
        CancellationToken cancellationToken = default)
    {
        var report = await LoadOwnedReportAsync(exportPlanId, diagnosisId, ownerUserId, true, cancellationToken);
        if (report?.ChatSession is null)
            return DiseaseOperationResult<DiagnosisDetailDto>.NotFound();
        if (report.Status is not (DiseaseAnalysisStatus.Draft or DiseaseAnalysisStatus.Ready))
            return ConflictDetail("يمكن إنهاء جلسة مفتوحة فقط.");

        var readiness = ReadReadiness(report);
        if (!readiness.IsReady && !force)
            return ConflictDetail("لا تزال هناك معلومات ناقصة. أكمل قائمة التحقق أو اختر التشخيص بالمعلومات الحالية.");

        report.FinalizedWithOverride = !readiness.IsReady;
        report.ContextSnapshotJson = await BuildPlanContextAsync(
            exportPlanId, ownerUserId, cancellationToken);
        report.Status = DiseaseAnalysisStatus.Waiting;
        report.ClosedAtUtc = timeProvider.GetUtcNow().UtcDateTime;
        report.ChatSession.IsActive = false;
        report.FailureMessage = null;
        await dbContext.SaveChangesAsync(cancellationToken);
        return DiseaseOperationResult<DiagnosisDetailDto>.Success(
            await LoadDetailAsync(diagnosisId, ownerUserId, cancellationToken, exportPlanId)
            ?? throw new InvalidOperationException("Finalized diagnosis could not be loaded."));
    }

    public async Task<DiseaseOperationResult<DiagnosisDetailDto>> CancelAsync(
        int exportPlanId,
        int diagnosisId,
        string ownerUserId,
        CancellationToken cancellationToken = default)
    {
        var report = await LoadOwnedReportAsync(exportPlanId, diagnosisId, ownerUserId, true, cancellationToken);
        if (report?.ChatSession is null)
            return DiseaseOperationResult<DiagnosisDetailDto>.NotFound();
        if (report.Status is not (DiseaseAnalysisStatus.Draft or DiseaseAnalysisStatus.Ready))
            return ConflictDetail("يمكن إلغاء جلسة مفتوحة فقط.");

        report.Status = DiseaseAnalysisStatus.Cancelled;
        report.ClosedAtUtc = timeProvider.GetUtcNow().UtcDateTime;
        report.ChatSession.IsActive = false;
        await dbContext.SaveChangesAsync(cancellationToken);
        return DiseaseOperationResult<DiagnosisDetailDto>.Success(
            await LoadDetailAsync(diagnosisId, ownerUserId, cancellationToken, exportPlanId)
            ?? throw new InvalidOperationException("Cancelled diagnosis could not be loaded."));
    }

    public async Task<DiseaseOperationResult<DiagnosisDetailDto>> RetryAsync(
        int exportPlanId,
        int diagnosisId,
        string ownerUserId,
        CancellationToken cancellationToken = default)
    {
        var report = await LoadOwnedReportAsync(exportPlanId, diagnosisId, ownerUserId, true, cancellationToken);
        if (report?.ChatSession is null)
            return DiseaseOperationResult<DiagnosisDetailDto>.NotFound();
        if (report.Status != DiseaseAnalysisStatus.Failed || report.ClosedAtUtc is null)
            return ConflictDetail("يمكن إعادة محاولة تشخيص مغلق وفاشل فقط.");

        report.Status = DiseaseAnalysisStatus.Waiting;
        report.FailureMessage = null;
        report.KnowledgeFailureMessage = null;
        await dbContext.SaveChangesAsync(cancellationToken);
        return DiseaseOperationResult<DiagnosisDetailDto>.Success(
            await LoadDetailAsync(diagnosisId, ownerUserId, cancellationToken, exportPlanId)
            ?? throw new InvalidOperationException("Retried diagnosis could not be loaded."));
    }

    public async Task<DiseaseOperationResult<DiseaseImageDownload>> DownloadImageAsync(
        int exportPlanId,
        int diagnosisId,
        int imageId,
        string ownerUserId,
        CancellationToken cancellationToken = default)
    {
        var image = await dbContext.DiseaseImages.AsNoTracking()
            .Where(item => item.Id == imageId && item.DiseaseReportId == diagnosisId &&
                item.DiseaseReport.ExportPlanId == exportPlanId &&
                item.DiseaseReport.ExportPlan.Farm.OwnerUserId == ownerUserId)
            .Select(item => new { item.StorageKey, item.ContentType, item.OriginalFileName })
            .SingleOrDefaultAsync(cancellationToken);
        if (image is null)
            return DiseaseOperationResult<DiseaseImageDownload>.NotFound();
        await using var source = await fileStorage.OpenReadAsync(image.StorageKey, cancellationToken);
        using var buffer = new MemoryStream();
        await source.CopyToAsync(buffer, cancellationToken);
        return DiseaseOperationResult<DiseaseImageDownload>.Success(
            new DiseaseImageDownload(buffer.ToArray(), image.ContentType, image.OriginalFileName));
    }

    public async Task<DiseaseOperationResult<TaskChangeProposalDto>> CreateTreatmentProposalAsync(
        int exportPlanId,
        int diagnosisId,
        string ownerUserId,
        CancellationToken cancellationToken = default)
    {
        var report = await LoadOwnedReportAsync(exportPlanId, diagnosisId, ownerUserId, false, cancellationToken);
        if (report is null)
            return DiseaseOperationResult<TaskChangeProposalDto>.NotFound();
        if (report.Status != DiseaseAnalysisStatus.Completed)
            return DiseaseOperationResult<TaskChangeProposalDto>.Conflict(ApiErrorCodes.Conflict, "يجب اكتمال التشخيص أولًا.");

        var active = report.Proposals
            .Where(item => item.Status == TaskChangeProposalStatus.PendingReview && item.ExpiresAtUtc > timeProvider.GetUtcNow().UtcDateTime)
            .OrderByDescending(item => item.Id)
            .FirstOrDefault();
        if (active is not null)
        {
            var existing = await proposalService.GetAsync(active.Id, ownerUserId, cancellationToken);
            return existing.Status == TaskProposalResultStatus.Succeeded
                ? DiseaseOperationResult<TaskChangeProposalDto>.Success(existing.Value!)
                : DiseaseOperationResult<TaskChangeProposalDto>.Conflict(ApiErrorCodes.Conflict, "يوجد اقتراح علاجي نشط بالفعل.");
        }
        if (report.Proposals.Any(item => item.Status == TaskChangeProposalStatus.Applied))
            return DiseaseOperationResult<TaskChangeProposalDto>.Conflict(ApiErrorCodes.Conflict, "تم تطبيق خطة هذا التشخيص بالفعل.");

        var context = await BuildPlanContextAsync(exportPlanId, ownerUserId, cancellationToken);
        // Diagnostic evidence is not sufficient to authorize pesticide work.
        // Keep chemical tasks fail-closed until this exact attempt includes
        // Egyptian registration, destination MRL, dose and PHI evidence.
        var hasApcEvidence = report.Sources.Any(source =>
            source.IsOfficialOrAcademic &&
            source.SourceUrl.Contains("apc.gov.eg", StringComparison.OrdinalIgnoreCase));
        var hasDestinationEvidence = report.Sources.Any(source =>
            source.IsOfficialOrAcademic &&
            (source.SourceUrl.Contains("food.ec.europa.eu", StringComparison.OrdinalIgnoreCase) ||
             source.SourceUrl.Contains("sfda.gov.sa", StringComparison.OrdinalIgnoreCase)));
        var hasDoseAndPhiEvidence = report.Sources.Any(source =>
            source.IsOfficialOrAcademic &&
            (source.Excerpt.Contains("dose", StringComparison.OrdinalIgnoreCase) ||
             source.Excerpt.Contains("جرعة", StringComparison.OrdinalIgnoreCase)) &&
            (source.Excerpt.Contains("PHI", StringComparison.OrdinalIgnoreCase) ||
             source.Excerpt.Contains("فترة الأمان", StringComparison.OrdinalIgnoreCase)));
        var chemicalTreatmentVerified = hasApcEvidence && hasDestinationEvidence && hasDoseAndPhiEvidence;
        var safeOnly = report.FinalizedWithOverride ||
                       report.ConfidencePercentage < 60 ||
                       !report.KnowledgeVerified ||
                       !chemicalTreatmentVerified;
        var request = $"""
            أنشئ مقترح خطة علاج ومتابعة كاملة للحالة التالية، من 3 إلى 6 مهام مستقبلية مترابطة.
            راجع سياق الخطة بالكامل: المناخ الموسمي عند إحداثيات المزرعة، المهام القادمة والسابقة، قياسات التربة، والتشخيصات السابقة.
            تجنب تكرار المهام الموجودة، ورتب المهام زمنيًا من الإجراء العاجل إلى المتابعة والتقييم.
            التشخيص: {report.SuspectedDisease}
            الشدة: {report.Severity}
            الثقة: {report.ConfidencePercentage}%
            الإجراءات: {report.RecommendedActions}
            {(safeOnly ? "مهم: اقترح مهام متابعة وفحص وعزل ونظافة واستشارة فقط. ممنوع أي مبيد أو مادة كيميائية أو جرعة." : "لا تقترح مادة كيميائية إلا إذا كانت موثقة صراحة في السياق.")}
            """;
        var generated = (await proposalGenerator.TryGenerateAsync(
                request, context, cancellationToken, exportPlanId))
            .Where(item => !safeOnly || item.ProposedValues.TaskTypeId != 3)
            .ToList();
        var usedSafeFallback = generated.Count < MinimumTreatmentPlanItems;
        if (usedSafeFallback)
            generated = await BuildSafeTreatmentPlanAsync(exportPlanId, report, cancellationToken);

        var created = await proposalService.CreateFromDiseaseAsync(
            exportPlanId, diagnosisId, generated, cancellationToken);
        // AI output can meet the item-count requirement while still failing the
        // proposal validator (for example, duplicate task/date slots). Preserve
        // the fail-closed validator and retry once with deterministic safe tasks.
        if (!usedSafeFallback &&
            created.Status == TaskProposalResultStatus.Invalid &&
            created.ErrorCode == ApiErrorCodes.TaskNotEligible)
        {
            generated = await BuildSafeTreatmentPlanAsync(exportPlanId, report, cancellationToken);
            created = await proposalService.CreateFromDiseaseAsync(
                exportPlanId, diagnosisId, generated, cancellationToken);
        }

        return created.Status switch
        {
            TaskProposalResultStatus.Succeeded when created.Value is not null =>
                DiseaseOperationResult<TaskChangeProposalDto>.Success(created.Value),
            TaskProposalResultStatus.NotFound => DiseaseOperationResult<TaskChangeProposalDto>.NotFound(),
            _ => DiseaseOperationResult<TaskChangeProposalDto>.Conflict(
                created.ErrorCode ?? ApiErrorCodes.Conflict,
                created.ErrorMessage ?? "تعذر إنشاء خطة علاجية آمنة.")
        };
    }

    private async Task<DiseaseReport?> LoadOwnedReportAsync(
        int exportPlanId, int diagnosisId, string ownerUserId, bool tracking,
        CancellationToken cancellationToken)
    {
        var query = dbContext.DiseaseReports
            .Include(item => item.ChatSession)!.ThenInclude(session => session!.Messages)
            .Include(item => item.Images)
            .Include(item => item.Sources)
            .Include(item => item.Proposals)
            .Where(item => item.Id == diagnosisId && item.ExportPlanId == exportPlanId &&
                item.ExportPlan.Farm.OwnerUserId == ownerUserId);
        if (!tracking) query = query.AsNoTracking();
        return await query.SingleOrDefaultAsync(cancellationToken);
    }

    private async Task<DiagnosisDetailDto?> LoadDetailAsync(
        int diagnosisId, string ownerUserId, CancellationToken cancellationToken, int? exportPlanId = null)
    {
        var report = await dbContext.DiseaseReports.AsNoTracking()
            .Include(item => item.ChatSession)!.ThenInclude(session => session!.Messages)
            .Include(item => item.Images)
            .Include(item => item.Sources)
            .Include(item => item.Proposals)
            .SingleOrDefaultAsync(item => item.Id == diagnosisId &&
                (!exportPlanId.HasValue || item.ExportPlanId == exportPlanId) &&
                item.ExportPlan.Farm.OwnerUserId == ownerUserId, cancellationToken);
        if (report is null) return null;

        return new DiagnosisDetailDto(
            MapSummary(report),
            ReadReadiness(report),
            ReadFacts(report),
            report.RecommendedActions,
            report.AdvisoryWarning,
            report.ExportImpact,
            report.FailureMessage,
            report.KnowledgeFailureMessage,
            report.Proposals.OrderByDescending(item => item.Id).Select(item => (int?)item.Id).FirstOrDefault(),
            report.ChatSession?.Messages.OrderBy(message => message.Id)
                .Select(message => MapMessage(message, report.Images)).ToList() ?? [],
            report.Sources.OrderByDescending(source => source.RetrievedAtUtc).Select(source => new DiagnosisSourceDto(
                source.Id, source.SourceName, source.SourceUrl, source.Publisher,
                source.SectionReference, source.Excerpt, source.RetrievedAtUtc,
                source.IsOfficialOrAcademic)).ToList());
    }

    private async Task<string> BuildPlanContextAsync(
        int exportPlanId, string ownerUserId, CancellationToken cancellationToken)
    {
        var plan = await dbContext.ExportPlans.AsNoTracking()
            .Where(item => item.Id == exportPlanId && item.Farm.OwnerUserId == ownerUserId)
            .Select(item => new
            {
                item.Id, item.Name, Crop = item.Crop.Name, Destination = item.Destination.Name,
                item.Governorate, item.City, item.Latitude, item.Longitude, item.AreaInFeddan, item.PlantingDate,
                item.ExpectedHarvestDate, item.ExpectedExportDate
            }).SingleAsync(cancellationToken);
        var today = DateOnly.FromDateTime(timeProvider.GetUtcNow().UtcDateTime);
        var futureTasks = await dbContext.FarmTasks.AsNoTracking()
            .Where(item => item.ExportPlanId == exportPlanId && item.PlannedDate >= today)
            .OrderBy(item => item.PlannedDate)
            .Select(item => new { item.Id, item.TaskTypeId, Type = item.TaskType.Name, item.Title,
                item.Description, item.Quantity, item.Unit, item.PlannedDate, item.Status, item.Priority })
            .ToListAsync(cancellationToken);
        var pastTasks = await dbContext.FarmTasks.AsNoTracking()
            .Where(item => item.ExportPlanId == exportPlanId && item.PlannedDate < today)
            .OrderByDescending(item => item.PlannedDate)
            .Select(item => new { item.Id, item.TaskTypeId, Type = item.TaskType.Name, item.Title,
                item.Description, item.Quantity, item.Unit, item.PlannedDate, item.Status, item.Priority })
            .ToListAsync(cancellationToken);
        var soil = await dbContext.SoilMeasurements.AsNoTracking()
            .Where(item => item.ExportPlanId == exportPlanId)
            .OrderByDescending(item => item.MeasurementDate).Take(5)
            .Select(item => new { item.MeasurementDate, item.PH, item.EC, item.Nitrogen,
                item.Phosphorus, item.Potassium, item.Moisture }).ToListAsync(cancellationToken);
        var previousDiagnoses = await dbContext.DiseaseReports.AsNoTracking()
            .Where(item => item.ExportPlanId == exportPlanId && item.Status == DiseaseAnalysisStatus.Completed)
            .OrderByDescending(item => item.CompletedAtUtc)
            .Select(item => new { item.Id, item.ObservationDate, item.SuspectedDisease,
                item.ConfidencePercentage, item.Severity, item.ObservedSymptoms, item.RecommendedActions })
            .ToListAsync(cancellationToken);
        ClimateDataSnapshot? climate = null;
        try
        {
            climate = await climateDataClient.GetClimateAsync(plan.Latitude, plan.Longitude, cancellationToken);
        }
        catch (Exception exception) when (exception is HttpRequestException or JsonException or InvalidDataException or TimeoutException)
        {
            // Climate enrichment must not prevent a safe treatment plan from being created.
        }

        var currentMonthClimate = climate?.Months.FirstOrDefault(item => item.Month == today.Month);
        var seasonalClimate = climate is null
            ? new TreatmentPlanClimateContext(false, null, null, null, "تعذر جلب المناخ الموسمي للموقع.")
            : new TreatmentPlanClimateContext(
                true,
                climate.SourceTitle,
                climate.SourceUrl,
                currentMonthClimate,
                "هذه بيانات مناخية موسمية تاريخية للموقع وليست نشرة طقس لحظية.");
        return JsonSerializer.Serialize(
            new { plan, today, seasonalClimate, futureTasks, pastTasks, soil, previousDiagnoses },
            JsonOptions);
    }

    private async Task<List<GeneratedTaskChangeItem>> BuildSafeTreatmentPlanAsync(
        int exportPlanId,
        DiseaseReport report,
        CancellationToken cancellationToken)
    {
        var today = DateOnly.FromDateTime(timeProvider.GetUtcNow().UtcDateTime);
        var latestDate = await dbContext.ExportPlans.AsNoTracking()
            .Where(item => item.Id == exportPlanId)
            .Select(item => item.ExpectedExportDate)
            .SingleAsync(cancellationToken);
        var taskTypes = await dbContext.TaskTypes.AsNoTracking()
            .Where(item => item.Name == "QualityCheck" || item.Name == "Pruning" || item.Name == "SoilTest")
            .ToDictionaryAsync(item => item.Name, item => item.Id, cancellationToken);
        var occupiedSlots = (await dbContext.FarmTasks.AsNoTracking()
                .Where(item => item.ExportPlanId == exportPlanId)
                .Select(item => new { item.TaskTypeId, item.PlannedDate })
                .ToListAsync(cancellationToken))
            .Select(item => (item.TaskTypeId, item.PlannedDate))
            .ToHashSet();
        var disease = report.SuspectedDisease ?? "الحالة المشتبه بها";
        var priority = report.Severity >= DiseaseSeverity.High ? TaskPriority.Critical : TaskPriority.High;
        var planned = new List<GeneratedTaskChangeItem>();

        AddSafeTask("QualityCheck", "فحص وعزل النباتات المصابة", $"فحص أعراض {disease} وعزل النباتات أو الثمار المصابة وتوثيق النطاق بالصور قبل أي إجراء إضافي.", 1, "زيارة", priority, "إجراء فوري آمن للحد من الانتشار.");
        AddSafeTask("Pruning", "إزالة الأجزاء المصابة وتعقيم الأدوات", "إزالة الأجزاء أو الثمار المصابة والتخلص منها بعيدًا عن الحقل، ثم تعقيم الأدوات وعدم نقل العدوى بين الخطوط.", 2, "عملية", TaskPriority.High, "تقليل مصدر العدوى دون استخدام مواد كيميائية.");
        AddSafeTask("SoilTest", "تقييم الرطوبة والصرف حول النباتات", "مراجعة الرطوبة والصرف والتهوية حول النباتات المصابة، وتسجيل أي ظروف تزيد انتشار المرض.", 3, "فحص", TaskPriority.Medium, "ربط المتابعة بظروف الحقل المسجلة.");
        AddSafeTask("QualityCheck", "زيارة متابعة وتقييم الاستجابة", $"إعادة فحص أعراض {disease} ومقارنة الصور لتحديد هل انحسر الانتشار أو يلزم الرجوع لمختص زراعي.", 7, "زيارة", priority, "متابعة الاستجابة وتحديد الحاجة لتصعيد آمن.");

        return planned;

        void AddSafeTask(
            string taskTypeName,
            string title,
            string description,
            int daysFromNow,
            string unit,
            TaskPriority taskPriority,
            string reason)
        {
            if (!taskTypes.TryGetValue(taskTypeName, out var taskTypeId)) return;
            var plannedDate = FindAvailableDate(taskTypeId, today.AddDays(daysFromNow), latestDate, occupiedSlots);
            if (plannedDate is null) return;
            planned.Add(new GeneratedTaskChangeItem(
                "add",
                null,
                new ProposedTaskValuesDto(taskTypeId, title, description, 1, unit, plannedDate.Value, taskPriority),
                reason));
        }
    }

    private static DateOnly? FindAvailableDate(
        int taskTypeId,
        DateOnly firstDate,
        DateOnly latestDate,
        ISet<(int TaskTypeId, DateOnly PlannedDate)> occupiedSlots)
    {
        for (var date = firstDate; date <= latestDate; date = date.AddDays(1))
        {
            if (occupiedSlots.Add((taskTypeId, date))) return date;
        }

        return null;
    }

    private sealed record TreatmentPlanClimateContext(
        bool IsAvailable,
        string? SourceTitle,
        string? SourceUrl,
        ClimateMonthSummary? CurrentMonth,
        string Note);

    private Task<bool> OwnsPlanAsync(int exportPlanId, string ownerUserId, CancellationToken cancellationToken) =>
        dbContext.ExportPlans.AnyAsync(item => item.Id == exportPlanId && item.Farm.OwnerUserId == ownerUserId, cancellationToken);

    private static DiagnosisSummaryDto MapSummary(DiseaseReport report) => new(
        report.Id, report.ExportPlanId,
        report.ChatSession?.Title ?? $"فحص {report.ObservationDate:yyyy-MM-dd}",
        report.Status, report.ObservationDate, report.SuspectedDisease,
        report.ConfidencePercentage, report.Severity, report.Images.Count,
        report.FinalizedWithOverride, report.KnowledgeVerified, report.CreatedAt,
        report.ClosedAtUtc, report.CompletedAtUtc);

    private static DiagnosisMessageDto MapMessage(ChatMessage message, IEnumerable<DiseaseImage> images) => new(
        message.Id, message.Role, message.Content, message.Status, message.CreatedAt,
        images.Where(image => image.ChatMessageId == message.Id).OrderBy(image => image.Id)
            .Select(image => new DiagnosisImageDto(image.Id, image.ChatMessageId,
                image.OriginalFileName, image.ContentType, image.FileSize)).ToList());

    private static DiagnosisFactsDto EmptyFacts() => new(null, null, null, null, null, null);

    private static DiagnosisFactsDto ReadFacts(DiseaseReport report)
    {
        var stored = string.IsNullOrWhiteSpace(report.CaseFactsJson)
            ? EmptyFacts()
            : JsonSerializer.Deserialize<DiagnosisFactsDto>(report.CaseFactsJson, JsonOptions) ?? EmptyFacts();

        var history = report.ChatSession?.Messages
            .Where(message => message.Status == ChatMessageStatus.Completed)
            .OrderBy(message => message.Id)
            .Select(message => new ConversationHistoryItem(message.Role, message.Content))
            .ToList() ?? [];
        return DiagnosisConversationState.RecoverFacts(stored, history, string.Empty);
    }

    private static DiagnosisReadinessDto ReadReadiness(DiseaseReport report) =>
        BuildReadiness(ReadFacts(report), report.Images.Count);

    private static DiagnosisReadinessDto BuildReadiness(DiagnosisFactsDto facts, int imageCount)
    {
        var items = new List<DiagnosisChecklistItemDto>
        {
            new("image", "صورة واضحة للحالة", imageCount > 0),
            new("affectedPart", "الجزء المصاب", Has(facts.AffectedPart)),
            new("onset", "بداية الأعراض ومدتها", Has(facts.Onset)),
            new("spreadPattern", "نمط انتشار الأعراض", Has(facts.SpreadPattern)),
            new("affectedExtent", "حجم أو نسبة الإصابة", Has(facts.AffectedExtent)),
            new("conditions", "ظروف الري أو الطقس المحيطة", Has(facts.Conditions)),
            new("previousTreatments", "المعاملات أو العلاجات السابقة", Has(facts.PreviousTreatments))
        };
        var missing = items.Where(item => item.IsRequired && !item.IsComplete).Select(item => item.Label).ToList();
        return new DiagnosisReadinessDto(missing.Count == 0, items, missing);
    }

    private static bool Has(string? value) => !string.IsNullOrWhiteSpace(value);
    private static string ReplyReference(int messageId) => $"{{\"replyToMessageId\":{messageId}}}";

    private static DiseaseOperationResult<DiagnosisTurnDto> InvalidTurn(string message) =>
        DiseaseOperationResult<DiagnosisTurnDto>.Invalid(ApiErrorCodes.DiseaseImageInvalid, message);
    private static DiseaseOperationResult<DiagnosisDetailDto> ConflictDetail(string message) =>
        DiseaseOperationResult<DiagnosisDetailDto>.Conflict(ApiErrorCodes.Conflict, message);

    private static (List<ValidatedImage> Images, string? Error) ValidateImages(IReadOnlyList<IncomingPrivateFile> images)
    {
        if (images.Count > MaximumImagesPerMessage || images.Sum(image => (long)image.Content.Length) > MaximumRequestBytes)
            return ([], "الحد الأقصى 5 صور و30 ميجابايت للرسالة.");
        var result = new List<ValidatedImage>();
        foreach (var image in images)
        {
            var detected = ImageFileSignature.Detect(image.Content);
            var original = Path.GetFileName(image.FileName);
            if (detected is null || image.Content.Length == 0 || image.Content.Length > MaximumImageBytes ||
                string.IsNullOrWhiteSpace(original) || original.Length > 255 ||
                !string.Equals(image.ClaimedContentType.Trim(), detected.Value.ContentType, StringComparison.OrdinalIgnoreCase))
            {
                return ([], "يُسمح بصور JPEG أو PNG صالحة حتى 10 ميجابايت للصورة.");
            }
            result.Add(new ValidatedImage(original, detected.Value.ContentType, detected.Value.Extension, image.Content));
        }
        return (result, null);
    }

    private sealed record ValidatedImage(string OriginalName, string ContentType, string Extension, byte[] Content);
}
