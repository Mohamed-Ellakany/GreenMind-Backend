namespace Greenmind.Application.Dtos.Dashboard;

public sealed record ExportPlanDashboardDto(
    ExportPlanDashboardOverviewDto ExportPlan,
    SeasonProgressDto Season,
    SoilMeasurementDashboardDto? LatestSoilMeasurement,
    TaskSummaryDto TaskSummary,
    IReadOnlyList<DashboardTaskDto> OverdueTasks,
    IReadOnlyList<DashboardTaskDto> UpcomingTasks,
    IReadOnlyList<DashboardTaskDto> CompletedTasks);

public sealed record ExportPlanDashboardOverviewDto(
    int Id,
    string Name,
    string Status,
    string Crop,
    string Destination,
    string Governorate,
    string City,
    decimal AreaInFeddan,
    DateOnly PlantingDate,
    DateOnly ExpectedHarvestDate,
    DateOnly ExpectedExportDate);

public sealed record SeasonProgressDto(
    int CurrentSeasonDay,
    int TotalSeasonDays,
    decimal ProgressPercentage,
    string GrowthStage);

public sealed record SoilMeasurementDashboardDto(
    DateOnly MeasurementDate,
    double Ph,
    double Ec,
    double Nitrogen,
    double Phosphorus,
    double Potassium,
    double Moisture,
    string? Notes);

public sealed record TaskSummaryDto(
    int Total,
    int Pending,
    int InProgress,
    int Completed,
    int Cancelled);

public sealed record DashboardTaskDto(
    int Id,
    string Title,
    string TaskType,
    DateOnly PlannedDate,
    DateOnly? ActualDate,
    string Status,
    string Priority);
