namespace Greenmind.Application.Dtos.Admin;

public sealed record AiUsageTotalsDto(
    long RequestCount,
    long InputTokens,
    long OutputTokens,
    long TotalTokens,
    decimal EstimatedCostUsd,
    decimal ActualCostUsd,
    long FallbackCount);

public sealed record AiUsageDailyDto(
    DateOnly Date,
    long RequestCount,
    long TotalTokens,
    decimal ActualCostUsd);

public sealed record AiUsageBreakdownDto(
    string Key,
    long RequestCount,
    long TotalTokens,
    decimal ActualCostUsd);

public sealed record AiUsageByUserDto(
    string? UserId,
    string UserName,
    string? Email,
    string? FarmName,
    long RequestCount,
    long TotalTokens,
    decimal ActualCostUsd);

public sealed record AiUsageRequestDto(
    int Id,
    string ProviderRequestId,
    string? UserId,
    string? UserName,
    string? FarmName,
    int? ExportPlanId,
    string? ExportPlanName,
    string Feature,
    string ModelId,
    string? Region,
    int InputTokens,
    int OutputTokens,
    int TotalTokens,
    string? StopReason,
    string? BudgetState,
    bool FallbackUsed,
    decimal EstimatedCostUsd,
    decimal ActualCostUsd,
    string ProviderStatus,
    DateTime CompletedAtUtc);

public sealed record AdminAiUsageOverviewDto(
    AiUsageTotalsDto Totals,
    IReadOnlyList<AiUsageDailyDto> DailyUsage,
    IReadOnlyList<AiUsageBreakdownDto> UsageByModel,
    IReadOnlyList<AiUsageBreakdownDto> UsageByFeature,
    IReadOnlyList<AiUsageByUserDto> UsageByUser,
    PagedResult<AiUsageRequestDto> Requests);

public sealed record AdminUserProfileDto(
    string Id,
    string FullName,
    string? UserName,
    string? Email,
    bool EmailConfirmed,
    string? PhoneNumber,
    bool PhoneNumberConfirmed,
    bool TwoFactorEnabled,
    bool IsActive,
    bool IsAiAccessSuspended,
    DateTime? LockoutEndUtc,
    DateTime? RegisteredAtUtc);

public sealed record AdminFarmDetailsDto(
    int Id,
    string Name,
    bool IsApproved,
    DateTime? ApprovedAtUtc,
    DateTime CreatedAt,
    int ExportPlanCount);

public sealed record AdminExportPlanDetailsDto(
    int Id,
    int FarmId,
    string FarmName,
    string Name,
    string Crop,
    string Destination,
    string Governorate,
    string City,
    double Latitude,
    double Longitude,
    decimal AreaInFeddan,
    DateOnly PlantingDate,
    DateOnly ExpectedHarvestDate,
    DateOnly ExpectedExportDate,
    string Status,
    string? Notes,
    string PlanningStatus,
    DateTime? PlanningRequestedAtUtc,
    DateTime? PlanningStartedAtUtc,
    DateTime? PlanningCompletedAtUtc,
    int PlanningAttemptCount,
    int GeneratedTaskCount,
    string? PlanningFailureMessage,
    int TaskCount,
    int DiseaseReportCount,
    DateTime CreatedAt);

public sealed record AdminUserAiUsageDto(
    AiUsageTotalsDto Totals,
    IReadOnlyList<AiUsageDailyDto> DailyUsage,
    IReadOnlyList<AiUsageBreakdownDto> UsageByModel,
    IReadOnlyList<AiUsageBreakdownDto> UsageByFeature,
    PagedResult<AiUsageRequestDto> Requests);

public sealed record AdminUserDetailsDto(
    AdminUserProfileDto User,
    IReadOnlyList<AdminFarmDetailsDto> Farms,
    IReadOnlyList<AdminExportPlanDetailsDto> ExportPlans,
    AdminUserAiUsageDto AiUsage);
