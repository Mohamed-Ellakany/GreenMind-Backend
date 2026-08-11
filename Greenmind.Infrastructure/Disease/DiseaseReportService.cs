using System.Security.Cryptography;
using System.Text;
using Greenmind.Application.Contracts.Api;
using Greenmind.Application.Contracts.Disease;
using Greenmind.Application.Contracts.Storage;
using Greenmind.Application.Dtos.Disease;
using Greenmind.Application.Interfaces.AI;
using Greenmind.Domain.Consts;
using Greenmind.Domain.Consts.Enums;
using Greenmind.Domain.Entities;
using Greenmind.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Greenmind.Infrastructure.Disease;

public sealed class DiseaseReportService(
    GreenmindDbContext dbContext,
    IPrivateFileStorage fileStorage,
    IDiseaseVisionAnalyzer visionAnalyzer,
    IDiagnosisKnowledgeProvider knowledgeProvider,
    TimeProvider timeProvider,
    ILogger<DiseaseReportService> logger) : IDiseaseReportService
{
    private const int MaximumImages = 5;
    private const int MaximumImageBytes = 10 * 1024 * 1024;
    private const int MaximumRequestBytes = 30 * 1024 * 1024;

    public async Task<DiseaseOperationResult<DiseaseReportDto>> SubmitAsync(
        int exportPlanId,
        string ownerUserId,
        CreateDiseaseReportRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!await OwnsPlanAsync(exportPlanId, ownerUserId, cancellationToken))
            return DiseaseOperationResult<DiseaseReportDto>.NotFound();

        if (request.ObservationDate == default ||
            request.ObservationDate > DateOnly.FromDateTime(timeProvider.GetUtcNow().UtcDateTime) ||
            request.AffectedArea?.Length > 500 ||
            request.Notes?.Length > 1000 ||
            request.Images.Count is < 1 or > MaximumImages ||
            request.Images.Sum(image => (long)image.Content.Length) > MaximumRequestBytes)
        {
            return InvalidImage("بيانات تقرير المرض أو عدد الصور غير صالح.");
        }

        var validatedImages = new List<ValidatedImage>(request.Images.Count);
        foreach (var image in request.Images)
        {
            var detected = ImageFileSignature.Detect(image.Content);
            var originalName = Path.GetFileName(image.FileName);
            if (detected is null ||
                image.Content.Length == 0 ||
                image.Content.Length > MaximumImageBytes ||
                string.IsNullOrWhiteSpace(originalName) ||
                originalName.Length > 255 ||
                !ContentTypeMatches(image.ClaimedContentType, detected.Value.ContentType))
            {
                return InvalidImage("يجب رفع صور JPEG أو PNG صالحة ضمن الحد المسموح.");
            }

            validatedImages.Add(new ValidatedImage(
                originalName,
                detected.Value.ContentType,
                detected.Value.Extension,
                image.Content));
        }

        var storedKeys = new List<string>(validatedImages.Count);
        try
        {
            var report = new DiseaseReport
            {
                ExportPlanId = exportPlanId,
                ObservationDate = request.ObservationDate,
                AffectedArea = Normalize(request.AffectedArea),
                UserNotes = Normalize(request.Notes),
                Status = DiseaseAnalysisStatus.Waiting
            };

            foreach (var image in validatedImages)
            {
                await using var content = new MemoryStream(image.Content, writable: false);
                var storageKey = await fileStorage.SaveAsync(
                    "disease",
                    image.Extension,
                    content,
                    cancellationToken);
                storedKeys.Add(storageKey);
                report.Images.Add(new DiseaseImage
                {
                    OriginalFileName = image.OriginalName,
                    StorageKey = storageKey,
                    ContentType = image.ContentType,
                    FileSize = image.Content.Length,
                    Checksum = Convert.ToHexString(SHA256.HashData(image.Content))
                });
            }

            dbContext.DiseaseReports.Add(report);
            await dbContext.SaveChangesAsync(cancellationToken);
            return DiseaseOperationResult<DiseaseReportDto>.Success(Map(report));
        }
        catch
        {
            foreach (var key in storedKeys)
            {
                try
                {
                    await fileStorage.DeleteAsync(key, CancellationToken.None);
                }
                catch
                {
                    // Preserve the original failure; orphan cleanup can be retried operationally.
                }
            }
            throw;
        }
    }

    public async Task<DiseaseOperationResult<DiseaseReportPageDto>> ListAsync(
        int exportPlanId,
        string ownerUserId,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        if (!await OwnsPlanAsync(exportPlanId, ownerUserId, cancellationToken))
            return DiseaseOperationResult<DiseaseReportPageDto>.NotFound();

        page = Math.Max(page, 1);
        pageSize = Math.Clamp(pageSize, 1, 100);
        var query = dbContext.DiseaseReports
            .AsNoTracking()
            .Include(report => report.Images)
            .Where(report => report.ExportPlanId == exportPlanId);
        var count = await query.CountAsync(cancellationToken);
        var reports = await query
            .OrderByDescending(report => report.ObservationDate)
            .ThenByDescending(report => report.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return DiseaseOperationResult<DiseaseReportPageDto>.Success(
            new DiseaseReportPageDto(
                reports.Select(Map).ToList(),
                page,
                pageSize,
                count));
    }

    public async Task<DiseaseOperationResult<DiseaseReportDto>> GetAsync(
        int exportPlanId,
        int reportId,
        string ownerUserId,
        CancellationToken cancellationToken = default)
    {
        var report = await LoadOwnedAsync(
            exportPlanId,
            reportId,
            ownerUserId,
            tracking: false,
            cancellationToken);
        return report is null
            ? DiseaseOperationResult<DiseaseReportDto>.NotFound()
            : DiseaseOperationResult<DiseaseReportDto>.Success(Map(report));
    }

    public async Task<DiseaseOperationResult<DiseaseImageDownload>> DownloadImageAsync(
        int exportPlanId,
        int reportId,
        int imageId,
        string ownerUserId,
        CancellationToken cancellationToken = default)
    {
        var image = await dbContext.DiseaseImages
            .AsNoTracking()
            .Where(item =>
                item.Id == imageId &&
                item.DiseaseReportId == reportId &&
                item.DiseaseReport.ExportPlanId == exportPlanId &&
                item.DiseaseReport.ExportPlan.Farm.OwnerUserId == ownerUserId)
            .Select(item => new
            {
                item.StorageKey,
                item.ContentType,
                item.OriginalFileName
            })
            .SingleOrDefaultAsync(cancellationToken);

        if (image is null)
            return DiseaseOperationResult<DiseaseImageDownload>.NotFound();

        await using var stream = await fileStorage.OpenReadAsync(
            image.StorageKey,
            cancellationToken);
        using var buffer = new MemoryStream();
        await stream.CopyToAsync(buffer, cancellationToken);
        return DiseaseOperationResult<DiseaseImageDownload>.Success(
            new DiseaseImageDownload(
                buffer.ToArray(),
                image.ContentType,
                image.OriginalFileName));
    }

    public async Task<DiseaseOperationResult<DiseaseReportDto>> RetryAsync(
        int exportPlanId,
        int reportId,
        string ownerUserId,
        CancellationToken cancellationToken = default)
    {
        var report = await LoadOwnedAsync(
            exportPlanId,
            reportId,
            ownerUserId,
            tracking: true,
            cancellationToken);
        if (report is null)
            return DiseaseOperationResult<DiseaseReportDto>.NotFound();
        if (report.Status != DiseaseAnalysisStatus.Failed)
        {
            return DiseaseOperationResult<DiseaseReportDto>.Conflict(
                ApiErrorCodes.Conflict,
                "يمكن إعادة محاولة التقارير التي فشل تحليلها فقط.");
        }

        report.Status = DiseaseAnalysisStatus.Waiting;
        report.FailureMessage = null;
        report.CompletedAtUtc = null;
        await dbContext.SaveChangesAsync(cancellationToken);
        return DiseaseOperationResult<DiseaseReportDto>.Success(Map(report));
    }

    public async Task<bool> ProcessNextPendingAsync(
        CancellationToken cancellationToken = default)
    {
        var report = await dbContext.DiseaseReports
            .Include(item => item.ExportPlan)
                .ThenInclude(plan => plan.Crop)
            .Include(item => item.ExportPlan)
                .ThenInclude(plan => plan.Destination)
            .Include(item => item.Images)
            .Include(item => item.Sources)
            .Include(item => item.ChatSession)!
                .ThenInclude(session => session!.Messages)
            .OrderBy(item => item.CreatedAt)
            .FirstOrDefaultAsync(
                item => item.Status == DiseaseAnalysisStatus.Waiting,
                cancellationToken);
        if (report is null)
            return false;

        report.Status = DiseaseAnalysisStatus.Analyzing;
        report.FailureMessage = null;
        await dbContext.SaveChangesAsync(cancellationToken);

        try
        {
            var images = new List<DiseaseVisionImage>(report.Images.Count);
            foreach (var storedImage in report.Images)
            {
                await using var stream = await fileStorage.OpenReadAsync(
                    storedImage.StorageKey,
                    cancellationToken);
                using var buffer = new MemoryStream();
                await stream.CopyToAsync(buffer, cancellationToken);
                images.Add(new DiseaseVisionImage(
                    storedImage.ContentType,
                    buffer.ToArray()));
            }

            var planContext = !string.IsNullOrWhiteSpace(report.ContextSnapshotJson)
                ? report.ContextSnapshotJson
                : await BuildPlanContextAsync(report.ExportPlan, cancellationToken);
            if (report.ChatSession is not null)
            {
                planContext += "\n\nحقائق جلسة التشخيص:\n" + (report.CaseFactsJson ?? "{}") +
                    "\n\nمحادثة التشخيص:\n" + string.Join("\n", report.ChatSession.Messages
                        .Where(message => message.Status == ChatMessageStatus.Completed)
                        .OrderBy(message => message.Id)
                        .Select(message => $"{message.Role}: {message.Content}"));
            }
            var request = new DiseaseVisionRequest(
                    planContext,
                    report.ObservationDate,
                    report.AffectedArea,
                    report.UserNotes,
                    images,
                    ExportPlanId: report.ExportPlanId);
            var result = await visionAnalyzer.AnalyzeAsync(request, cancellationToken);

            if (report.ChatSessionId.HasValue)
            {
                var cropKnowledgeKey = CropCatalog.GetDiagnosisKnowledgeKey(report.ExportPlan.CropId)
                    ?? throw new InvalidOperationException(
                        "The crop does not support trusted smart-diagnosis knowledge.");
                var cropLookupKeys = CropCatalog.GetLookupKeys(cropKnowledgeKey);
                var knowledge = await knowledgeProvider.SearchAsync(
                    cropKnowledgeKey,
                    result.SuspectedDisease,
                    cancellationToken);
                report.KnowledgeVerified = knowledge.IsVerified;
                report.KnowledgeFailureMessage = knowledge.FailureMessage;
                dbContext.DiseaseReportSources.RemoveRange(report.Sources);
                report.Sources.Clear();
                foreach (var source in knowledge.Sources)
                {
                    report.Sources.Add(new DiseaseReportSource
                    {
                        SourceName = source.SourceName,
                        SourceUrl = source.SourceUrl,
                        Publisher = source.Publisher,
                        SectionReference = source.SectionReference,
                        Excerpt = source.Excerpt,
                        ContentHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(source.Excerpt))),
                        RetrievedAtUtc = source.RetrievedAtUtc,
                        IsOfficialOrAcademic = source.IsOfficialOrAcademic
                    });

                    var cached = await dbContext.ComplianceDocuments.FirstOrDefaultAsync(
                        item => item.Category == ComplianceCategory.DiseaseTreatment &&
                                item.Crop != null && cropLookupKeys.Contains(item.Crop) &&
                                item.SourceUrl == source.SourceUrl,
                        cancellationToken);
                    if (cached is null)
                    {
                        dbContext.ComplianceDocuments.Add(new ComplianceDocument
                        {
                            Category = ComplianceCategory.DiseaseTreatment,
                            Crop = cropKnowledgeKey,
                            SourceName = source.SourceName,
                            SourceUrl = source.SourceUrl,
                            Publisher = source.Publisher,
                            SectionReference = source.SectionReference,
                            Content = source.Excerpt,
                            Embedding = "[]",
                            RetrievedAtUtc = source.RetrievedAtUtc,
                            TrustLevel = "Official"
                        });
                    }
                    else
                    {
                        cached.Content = source.Excerpt;
                        cached.RetrievedAtUtc = source.RetrievedAtUtc;
                        cached.SectionReference = source.SectionReference;
                    }
                }

                if (knowledge.IsVerified)
                    result = await visionAnalyzer.AnalyzeAsync(request with { KnowledgeContext = knowledge.Context }, cancellationToken);
            }
            ValidateAnalysis(result);

            report.SuspectedDisease = result.SuspectedDisease.Trim();
            report.ConfidencePercentage = result.ConfidencePercentage;
            report.Severity = result.Severity;
            report.ObservedSymptoms = result.ObservedSymptoms.Trim();
            report.RecommendedActions = result.RecommendedActions.Trim();
            report.ExportImpact = result.ExportImpact.Trim();
            report.AdvisoryWarning = EnsureAdvisoryWarning(result);
            report.ModelName = result.ModelName;
            report.PromptVersion = result.PromptVersion;
            report.Status = DiseaseAnalysisStatus.Completed;
            report.CompletedAtUtc = timeProvider.GetUtcNow().UtcDateTime;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            report.Status = DiseaseAnalysisStatus.Waiting;
            await dbContext.SaveChangesAsync(CancellationToken.None);
            throw;
        }
        catch (Exception exception)
        {
            logger.LogError(
                exception,
                "Disease image analysis failed for report {ReportId} in export plan {ExportPlanId}.",
                report.Id,
                report.ExportPlanId);
            report.Status = DiseaseAnalysisStatus.Failed;
            report.FailureMessage = AnalysisFailureMessage(exception);
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }

    private static string AnalysisFailureMessage(Exception exception) => exception switch
    {
        InvalidDataException =>
            "تعذر التحقق من نتيجة التحليل. يمكنك إعادة المحاولة.",
        InvalidOperationException =>
            "خدمة تحليل الصور غير مهيأة. راجع إعداد مفتاح ITI ثم أعد المحاولة.",
        HttpRequestException { StatusCode: System.Net.HttpStatusCode.Unauthorized or System.Net.HttpStatusCode.Forbidden } =>
            "رفضت خدمة التحليل المفتاح أو النموذج المحدد. راجع صلاحيات ITI ثم أعد المحاولة.",
        HttpRequestException { StatusCode: System.Net.HttpStatusCode.TooManyRequests } =>
            "حصة خدمة التحليل مستنفدة أو مشغولة حاليًا. أعد المحاولة لاحقًا.",
        HttpRequestException { StatusCode: >= System.Net.HttpStatusCode.InternalServerError } =>
            "خدمة تحليل الصور غير متاحة مؤقتًا. أعد المحاولة لاحقًا.",
        HttpRequestException =>
            "تعذر الاتصال بخدمة تحليل الصور. تحقق من إعداد البوابة ثم أعد المحاولة.",
        _ =>
            "تعذر تحليل الصور حاليًا. يمكنك إعادة المحاولة لاحقًا."
    };

    private async Task<string> BuildPlanContextAsync(
        ExportPlan plan,
        CancellationToken cancellationToken)
    {
        var tasks = await dbContext.FarmTasks
            .AsNoTracking()
            .Where(task => task.ExportPlanId == plan.Id)
            .OrderBy(task => task.PlannedDate)
            .Take(30)
            .Select(task => $"{task.PlannedDate:yyyy-MM-dd}: {task.Title} ({task.Status})")
            .ToListAsync(cancellationToken);
        var soil = await dbContext.SoilMeasurements
            .AsNoTracking()
            .Where(item => item.ExportPlanId == plan.Id)
            .OrderByDescending(item => item.MeasurementDate)
            .Take(5)
            .Select(item => $"{item.MeasurementDate:yyyy-MM-dd}: pH={item.PH}, N={item.Nitrogen}, P={item.Phosphorus}, K={item.Potassium}")
            .ToListAsync(cancellationToken);

        return $"""
            الخطة: {plan.Name}
            المحصول: {plan.Crop.Name}
            الوجهة: {plan.Destination.Name}
            الموقع: {plan.Governorate} - {plan.City}
            المساحة: {plan.AreaInFeddan} فدان
            الزراعة: {plan.PlantingDate:yyyy-MM-dd}
            الحصاد المتوقع: {plan.ExpectedHarvestDate:yyyy-MM-dd}
            المهام:\n{string.Join("\n", tasks)}
            قياسات التربة:\n{string.Join("\n", soil)}
            """;
    }

    private async Task<DiseaseReport?> LoadOwnedAsync(
        int exportPlanId,
        int reportId,
        string ownerUserId,
        bool tracking,
        CancellationToken cancellationToken)
    {
        var query = dbContext.DiseaseReports
            .Include(report => report.Images)
            .Where(report =>
                report.Id == reportId &&
                report.ExportPlanId == exportPlanId &&
                report.ExportPlan.Farm.OwnerUserId == ownerUserId);
        if (!tracking)
            query = query.AsNoTracking();
        return await query.SingleOrDefaultAsync(cancellationToken);
    }

    private Task<bool> OwnsPlanAsync(
        int exportPlanId,
        string ownerUserId,
        CancellationToken cancellationToken) =>
        dbContext.ExportPlans.AnyAsync(
            plan =>
                plan.Id == exportPlanId &&
                plan.Farm.OwnerUserId == ownerUserId,
            cancellationToken);

    private static DiseaseOperationResult<DiseaseReportDto> InvalidImage(string message) =>
        DiseaseOperationResult<DiseaseReportDto>.Invalid(
            ApiErrorCodes.DiseaseImageInvalid,
            message);

    private static void ValidateAnalysis(DiseaseVisionResult result)
    {
        if (string.IsNullOrWhiteSpace(result.SuspectedDisease) ||
            result.SuspectedDisease.Length > 300 ||
            result.ConfidencePercentage is < 0 or > 100 ||
            !Enum.IsDefined(result.Severity) ||
            string.IsNullOrWhiteSpace(result.ObservedSymptoms) ||
            string.IsNullOrWhiteSpace(result.RecommendedActions) ||
            string.IsNullOrWhiteSpace(result.ExportImpact) ||
            string.IsNullOrWhiteSpace(result.AdvisoryWarning) ||
            result.AdvisoryWarning.Length > 1000 ||
            string.IsNullOrWhiteSpace(result.ModelName) ||
            result.ModelName.Length > 100 ||
            string.IsNullOrWhiteSpace(result.PromptVersion) ||
            result.PromptVersion.Length > 50)
        {
            throw new InvalidDataException("Invalid disease analysis result.");
        }
    }

    private static string EnsureAdvisoryWarning(DiseaseVisionResult result)
    {
        var warning = result.AdvisoryWarning.Trim();
        if (result.ConfidencePercentage < 60 || result.Severity >= DiseaseSeverity.High)
        {
            const string professionalAdvice = " يُنصح بفحص متخصص وأخذ عينة معملية قبل اتخاذ قرار علاجي.";
            if (!warning.Contains("متخصص", StringComparison.Ordinal))
                warning += professionalAdvice;
        }
        return warning.Length <= 1000 ? warning : warning[..1000];
    }

    private static bool ContentTypeMatches(string claimed, string detected) =>
        string.Equals(
            claimed.Trim(),
            detected,
            StringComparison.OrdinalIgnoreCase) ||
        (detected == "image/jpeg" &&
         string.Equals(claimed.Trim(), "image/jpg", StringComparison.OrdinalIgnoreCase));

    private static DiseaseReportDto Map(DiseaseReport report) => new(
        report.Id,
        report.ExportPlanId,
        report.ObservationDate,
        report.AffectedArea,
        report.UserNotes,
        report.Status,
        report.SuspectedDisease,
        report.ConfidencePercentage,
        report.Severity,
        report.ObservedSymptoms,
        report.RecommendedActions,
        report.ExportImpact,
        report.AdvisoryWarning,
        report.FailureMessage,
        report.CompletedAtUtc,
        report.CreatedAt,
        report.Images
            .OrderBy(image => image.Id)
            .Select(image => new DiseaseImageDto(
                image.Id,
                image.OriginalFileName,
                image.ContentType,
                image.FileSize))
            .ToList());

    private static string? Normalize(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private sealed record ValidatedImage(
        string OriginalName,
        string ContentType,
        string Extension,
        byte[] Content);
}
