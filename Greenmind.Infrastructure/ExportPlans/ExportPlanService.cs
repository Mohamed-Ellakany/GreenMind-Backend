using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Greenmind.Application.Contracts.ExportPlans;
using Greenmind.Application.Dtos.ExportPlans;
using Greenmind.Domain.Consts;
using Greenmind.Domain.Entities;
using Greenmind.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace Greenmind.Infrastructure.ExportPlans;

public sealed class ExportPlanService(
    GreenmindDbContext dbContext,
    IExportScheduleAdvisor scheduleAdvisor,
    IConfiguration configuration,
    TimeProvider timeProvider) : IExportPlanService
{
    public async Task<ExportPlanResult<ExportPlanDetailsDto>> CreateAsync(
        string ownerUserId,
        ExportPlanCreateRequest request,
        CancellationToken cancellationToken = default)
    {
        var cropId = request.CropId.GetValueOrDefault();
        var destinationId = request.DestinationId.GetValueOrDefault();

        var farmId = await dbContext.Farms
            .AsNoTracking()
            .Where(farm => farm.OwnerUserId == ownerUserId)
            .Select(farm => (int?)farm.Id)
            .SingleOrDefaultAsync(cancellationToken);

        if (!farmId.HasValue)
        {
            return ExportPlanResult<ExportPlanDetailsDto>.NotFound();
        }

        if (!await dbContext.Crops.AnyAsync(
                crop => crop.Id == cropId && crop.IsActive && !crop.IsDeleted,
                cancellationToken))
        {
            return ExportPlanResult<ExportPlanDetailsDto>.InvalidReference(
                nameof(request.CropId),
                "Crop must refer to an active record.");
        }

        if (!await dbContext.Destinations.AnyAsync(
                destination => destination.Id == destinationId && destination.IsActive && !destination.IsDeleted,
                cancellationToken))
        {
            return ExportPlanResult<ExportPlanDetailsDto>.InvalidReference(
                nameof(request.DestinationId),
                "Destination must refer to an active record.");
        }

        var tokenValidation = ValidateAnalysisToken(ownerUserId, request);
        if (tokenValidation.Error is not null)
        {
            return ExportPlanResult<ExportPlanDetailsDto>.InvalidReference(
                nameof(request.AnalysisToken),
                tokenValidation.Error);
        }

        if (tokenValidation.Payload is { } token &&
            (token.Assessment != "Feasible" || token.Confidence == "Low") &&
            !request.AcceptScheduleWarnings)
        {
            return ExportPlanResult<ExportPlanDetailsDto>.InvalidReference(
                nameof(request.AcceptScheduleWarnings),
                "Schedule warnings and low-confidence inferences must be acknowledged.");
        }

        var exportPlan = new ExportPlan
        {
            FarmId = farmId.Value,
            CropId = cropId,
            DestinationId = destinationId,
            Name = request.Name!.Trim(),
            Governorate = request.Governorate!.Trim(),
            City = request.City!.Trim(),
            Latitude = request.Latitude!.Value,
            Longitude = request.Longitude!.Value,
            AreaInFeddan = request.AreaInFeddan!.Value,
            PlantingDate = request.PlantingDate!.Value,
            ExpectedHarvestDate = request.ExpectedHarvestDate!.Value,
            ExpectedExportDate = request.ExpectedExportDate!.Value,
            Status = Greenmind.Domain.Consts.Enums.ExportPlanStatus.Planning,
            Notes = NormalizeNotes(request.Notes),
            PlanningStatus = Greenmind.Domain.Consts.Enums.PlanningGenerationStatus.Pending,
            PlanningRequestedAtUtc = DateTime.UtcNow,
            NextPlanningAttemptAtUtc = DateTime.UtcNow
        };

        dbContext.ExportPlans.Add(exportPlan);
        await dbContext.SaveChangesAsync(cancellationToken);

        return ExportPlanResult<ExportPlanDetailsDto>.Success(await MapDetailsAsync(exportPlan.Id, cancellationToken));
    }

    public async Task<ExportPlanResult<ExportPlanScheduleAnalysisDto>> AnalyzeScheduleAsync(
        string ownerUserId,
        ExportPlanScheduleAnalysisRequest request,
        CancellationToken cancellationToken = default)
    {
        var farm = await dbContext.Farms
            .AsNoTracking()
            .Where(item => item.OwnerUserId == ownerUserId)
            .Select(item => new { item.Id })
            .SingleOrDefaultAsync(cancellationToken);
        if (farm is null)
            return ExportPlanResult<ExportPlanScheduleAnalysisDto>.NotFound();

        var crop = await dbContext.Crops
            .AsNoTracking()
            .Where(item => item.Id == request.CropId && item.IsActive && !item.IsDeleted)
            .Select(item => new { item.Name, item.Description })
            .SingleOrDefaultAsync(cancellationToken);
        if (crop is null)
        {
            return ExportPlanResult<ExportPlanScheduleAnalysisDto>.InvalidReference(
                nameof(request.CropId),
                "Crop must refer to an active record.");
        }

        var destination = await dbContext.Destinations
            .AsNoTracking()
            .Where(item => item.Id == request.DestinationId && item.IsActive && !item.IsDeleted)
            .Select(item => new { item.Name, item.Description })
            .SingleOrDefaultAsync(cancellationToken);
        if (destination is null)
        {
            return ExportPlanResult<ExportPlanScheduleAnalysisDto>.InvalidReference(
                nameof(request.DestinationId),
                "Destination must refer to an active record.");
        }

        var history = await dbContext.ExportPlans
            .AsNoTracking()
            .Where(item => item.FarmId == farm.Id && item.CropId == request.CropId)
            .OrderByDescending(item => item.Id)
            .Take(8)
            .Select(item => new HistoricalExportScheduleDto(
                item.PlantingDate,
                item.ExpectedHarvestDate,
                item.ExpectedExportDate,
                item.Status.ToString()))
            .ToListAsync(cancellationToken);

        var today = CairoToday();
        if (request.PlantingDate.HasValue && request.PlantingDate.Value < today)
        {
            return ExportPlanResult<ExportPlanScheduleAnalysisDto>.InvalidReference(
                nameof(request.PlantingDate),
                "Planting date cannot be in the past.");
        }

        var result = await scheduleAdvisor.AnalyzeAsync(
            new ExportScheduleAdvisorContext(
                crop.Name,
                crop.Description,
                destination.Name,
                destination.Description,
                request.Governorate!.Trim(),
                request.City!.Trim(),
                request.Latitude!.Value,
                request.Longitude!.Value,
                request.AreaInFeddan!.Value,
                NormalizeNotes(request.Notes),
                today,
                request.PlantingDate,
                request.ExpectedHarvestDate,
                request.ExpectedExportDate,
                history,
                ownerUserId,
                farm.Id),
            cancellationToken);

        return ExportPlanResult<ExportPlanScheduleAnalysisDto>.Success(
            new ExportPlanScheduleAnalysisDto(
                result.SeasonName,
                result.PlantingDate,
                result.ExpectedHarvestDate,
                result.ExpectedExportDate,
                result.Assessment,
                result.Confidence,
                result.Summary,
                result.Reasons,
                result.Warnings,
                result.Recommendations,
                result.Assumptions,
                result.Sources,
                result.InferredFacts,
                CreateAnalysisToken(ownerUserId, request, result)));
    }

    public async Task<IReadOnlyList<ExportPlanListItemDto>> GetAllAsync(
        string ownerUserId,
        CancellationToken cancellationToken = default) =>
        await dbContext.ExportPlans
            .AsNoTracking()
            .Where(plan => plan.Farm.OwnerUserId == ownerUserId)
            .OrderBy(plan => plan.ExpectedExportDate)
            .ThenBy(plan => plan.Id)
            .Select(plan => new ExportPlanListItemDto(
                plan.Id,
                plan.Name,
                plan.Crop.Name,
                plan.Destination.Name,
                plan.PlantingDate,
                plan.ExpectedHarvestDate,
                plan.ExpectedExportDate,
                plan.Status.ToString(),
                plan.PlanningStatus.ToString(),
                plan.GeneratedTaskCount))
            .ToListAsync(cancellationToken);

    public async Task<ExportPlanResult<ExportPlanDetailsDto>> GetByIdAsync(
        int exportPlanId,
        string ownerUserId,
        CancellationToken cancellationToken = default)
    {
        var plan = await dbContext.ExportPlans
            .AsNoTracking()
            .Where(item => item.Id == exportPlanId && item.Farm.OwnerUserId == ownerUserId)
            .Select(item => new ExportPlanDetailsDto(
                item.Id,
                item.Name,
                item.CropId,
                item.Crop.Name,
                item.CropId == CropCatalog.Strawberry.Id,
                item.DestinationId,
                item.Destination.Name,
                item.Governorate,
                item.City,
                item.Latitude,
                item.Longitude,
                item.AreaInFeddan,
                item.PlantingDate,
                item.ExpectedHarvestDate,
                item.ExpectedExportDate,
                item.Status.ToString(),
                item.Notes,
                item.PlanningStatus.ToString(),
                item.GeneratedTaskCount,
                item.PlanningFailureMessage))
            .SingleOrDefaultAsync(cancellationToken);

        return plan is null
            ? ExportPlanResult<ExportPlanDetailsDto>.NotFound()
            : ExportPlanResult<ExportPlanDetailsDto>.Success(plan);
    }

    public async Task<ExportPlanResult<PlanningStatusDto>> GetPlanningStatusAsync(
        int exportPlanId,
        string ownerUserId,
        CancellationToken cancellationToken = default)
    {
        var plan = await dbContext.ExportPlans
            .AsNoTracking()
            .Where(plan => plan.Id == exportPlanId && plan.Farm.OwnerUserId == ownerUserId)
            .Select(plan => new PlanningStatusDto(
                plan.Id,
                plan.PlanningStatus.ToString(),
                plan.PlanningAttemptCount,
                plan.GeneratedTaskCount,
                plan.PlanningRequestedAtUtc,
                plan.PlanningStartedAtUtc,
                plan.PlanningCompletedAtUtc,
                plan.PlanningFailureMessage))
            .SingleOrDefaultAsync(cancellationToken);

        return plan is null
            ? ExportPlanResult<PlanningStatusDto>.NotFound()
            : ExportPlanResult<PlanningStatusDto>.Success(plan);
    }

    public async Task<ExportPlanResult<PlanningStatusDto>> RetryPlanningAsync(
        int exportPlanId,
        string ownerUserId,
        CancellationToken cancellationToken = default)
    {
        var plan = await dbContext.ExportPlans
            .SingleOrDefaultAsync(item =>
                item.Id == exportPlanId && item.Farm.OwnerUserId == ownerUserId,
                cancellationToken);

        if (plan is null)
        {
            return ExportPlanResult<PlanningStatusDto>.NotFound();
        }

        if (plan.PlanningStatus != Greenmind.Domain.Consts.Enums.PlanningGenerationStatus.Failed)
        {
            return ExportPlanResult<PlanningStatusDto>.InvalidReference(
                nameof(plan.PlanningStatus),
                "Only failed planning jobs can be retried.");
        }

        plan.PlanningStatus = Greenmind.Domain.Consts.Enums.PlanningGenerationStatus.Pending;
        plan.PlanningRequestedAtUtc = DateTime.UtcNow;
        plan.PlanningStartedAtUtc = null;
        plan.PlanningCompletedAtUtc = null;
        plan.NextPlanningAttemptAtUtc = DateTime.UtcNow;
        plan.PlanningAttemptCount = 0;
        plan.PlanningFailureMessage = null;
        await dbContext.SaveChangesAsync(cancellationToken);

        return ExportPlanResult<PlanningStatusDto>.Success(MapPlanningStatus(plan));
    }

    private async Task<ExportPlanDetailsDto> MapDetailsAsync(int exportPlanId, CancellationToken cancellationToken) =>
        (await dbContext.ExportPlans
            .AsNoTracking()
            .Where(plan => plan.Id == exportPlanId)
            .Select(plan => new ExportPlanDetailsDto(
                plan.Id,
                plan.Name,
                plan.CropId,
                plan.Crop.Name,
                plan.CropId == CropCatalog.Strawberry.Id,
                plan.DestinationId,
                plan.Destination.Name,
                plan.Governorate,
                plan.City,
                plan.Latitude,
                plan.Longitude,
                plan.AreaInFeddan,
                plan.PlantingDate,
                plan.ExpectedHarvestDate,
                plan.ExpectedExportDate,
                plan.Status.ToString(),
                plan.Notes,
                plan.PlanningStatus.ToString(),
                plan.GeneratedTaskCount,
                plan.PlanningFailureMessage))
            .SingleAsync(cancellationToken));

    private static PlanningStatusDto MapPlanningStatus(ExportPlan plan) => new(
        plan.Id,
        plan.PlanningStatus.ToString(),
        plan.PlanningAttemptCount,
        plan.GeneratedTaskCount,
        plan.PlanningRequestedAtUtc,
        plan.PlanningStartedAtUtc,
        plan.PlanningCompletedAtUtc,
        plan.PlanningFailureMessage);

    private static string? NormalizeNotes(string? notes) =>
        string.IsNullOrWhiteSpace(notes) ? null : notes.Trim();

    private string CreateAnalysisToken(
        string ownerUserId,
        ExportPlanScheduleAnalysisRequest request,
        ExportScheduleAdvisorResult result)
    {
        var payload = new AnalysisTokenPayload(
            ownerUserId,
            request.Name!.Trim(),
            request.CropId!.Value,
            request.DestinationId!.Value,
            request.Governorate!.Trim(),
            request.City!.Trim(),
            request.Latitude!.Value,
            request.Longitude!.Value,
            request.AreaInFeddan!.Value,
            NormalizeNotes(request.Notes),
            result.PlantingDate,
            result.ExpectedHarvestDate,
            result.ExpectedExportDate,
            result.Assessment,
            result.Confidence,
            timeProvider.GetUtcNow().UtcDateTime);
        var bytes = JsonSerializer.SerializeToUtf8Bytes(payload);
        var signature = HMACSHA256.HashData(TokenKey(), bytes);
        return $"{Base64UrlEncode(bytes)}.{Base64UrlEncode(signature)}";
    }

    private (AnalysisTokenPayload? Payload, string? Error) ValidateAnalysisToken(
        string ownerUserId,
        ExportPlanCreateRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.AnalysisToken))
            return (null, "Analyze the schedule before creating the export plan.");

        var parts = request.AnalysisToken.Split('.', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length != 2)
            return (null, "The schedule analysis token is invalid.");

        try
        {
            var bytes = Base64UrlDecode(parts[0]);
            var suppliedSignature = Base64UrlDecode(parts[1]);
            var expectedSignature = HMACSHA256.HashData(TokenKey(), bytes);
            if (!CryptographicOperations.FixedTimeEquals(suppliedSignature, expectedSignature))
                return (null, "The schedule analysis token is invalid.");

            var payload = JsonSerializer.Deserialize<AnalysisTokenPayload>(bytes);
            if (payload is null || payload.OwnerUserId != ownerUserId)
                return (null, "The schedule analysis token does not belong to this account.");
            if (timeProvider.GetUtcNow().UtcDateTime - payload.IssuedAtUtc > TimeSpan.FromMinutes(30))
                return (null, "The schedule analysis expired. Analyze the dates again.");

            var matches = payload.Name == request.Name?.Trim()
                && payload.CropId == request.CropId
                && payload.DestinationId == request.DestinationId
                && payload.Governorate == request.Governorate?.Trim()
                && payload.City == request.City?.Trim()
                && Math.Abs(payload.Latitude - request.Latitude.GetValueOrDefault()) < 0.0000005
                && Math.Abs(payload.Longitude - request.Longitude.GetValueOrDefault()) < 0.0000005
                && payload.AreaInFeddan == request.AreaInFeddan
                && payload.Notes == NormalizeNotes(request.Notes)
                && payload.PlantingDate == request.PlantingDate
                && payload.ExpectedHarvestDate == request.ExpectedHarvestDate
                && payload.ExpectedExportDate == request.ExpectedExportDate;
            return matches
                ? (payload, null)
                : (null, "Plan data or coordinates changed after analysis. Analyze the schedule again.");
        }
        catch (Exception exception) when (exception is FormatException or JsonException)
        {
            return (null, "The schedule analysis token is invalid.");
        }
    }

    private byte[] TokenKey()
    {
        var configured = configuration["ScheduleAnalysis:SigningKey"]
            ?? configuration["Jwt:Key"]
            ?? "greenmind_development_only_key_32_bytes";
        return SHA256.HashData(Encoding.UTF8.GetBytes(configured));
    }

    private DateOnly CairoToday()
    {
        TimeZoneInfo cairo;
        try { cairo = TimeZoneInfo.FindSystemTimeZoneById("Africa/Cairo"); }
        catch (TimeZoneNotFoundException) { cairo = TimeZoneInfo.FindSystemTimeZoneById("Egypt Standard Time"); }
        var local = TimeZoneInfo.ConvertTime(timeProvider.GetUtcNow(), cairo);
        return DateOnly.FromDateTime(local.DateTime);
    }

    private static string Base64UrlEncode(byte[] value) =>
        Convert.ToBase64String(value).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    private static byte[] Base64UrlDecode(string value)
    {
        var padded = value.Replace('-', '+').Replace('_', '/');
        padded += new string('=', (4 - padded.Length % 4) % 4);
        return Convert.FromBase64String(padded);
    }

    private sealed record AnalysisTokenPayload(
        string OwnerUserId,
        string Name,
        int CropId,
        int DestinationId,
        string Governorate,
        string City,
        double Latitude,
        double Longitude,
        decimal AreaInFeddan,
        string? Notes,
        DateOnly PlantingDate,
        DateOnly ExpectedHarvestDate,
        DateOnly ExpectedExportDate,
        string Assessment,
        string Confidence,
        DateTime IssuedAtUtc);
}
