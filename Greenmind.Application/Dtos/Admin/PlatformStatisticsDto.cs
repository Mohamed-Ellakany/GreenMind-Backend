namespace Greenmind.Application.Dtos.Admin;

public sealed record PlatformStatisticsDto(
    int TotalFarms,
    int PendingFarms,
    int ActiveFarms,
    int InactiveFarms,
    int TotalCrops,
    int TotalDestinations,
    int TotalExportPlans,
    int TotalTasks,
    IReadOnlyList<ExportPlanBreakdownDto> ExportPlansByCrop,
    IReadOnlyList<ExportPlanBreakdownDto> ExportPlansByDestination);

public sealed record ExportPlanBreakdownDto(
    int Id,
    string Name,
    int ExportPlanCount);
