using Greenmind.Application.Dtos.Disease;

namespace Greenmind.Application.Contracts.Disease;

public interface IDiseaseReportService
{
    Task<DiseaseOperationResult<DiseaseReportDto>> SubmitAsync(
        int exportPlanId,
        string ownerUserId,
        CreateDiseaseReportRequest request,
        CancellationToken cancellationToken = default);

    Task<DiseaseOperationResult<DiseaseReportPageDto>> ListAsync(
        int exportPlanId,
        string ownerUserId,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default);

    Task<DiseaseOperationResult<DiseaseReportDto>> GetAsync(
        int exportPlanId,
        int reportId,
        string ownerUserId,
        CancellationToken cancellationToken = default);

    Task<DiseaseOperationResult<DiseaseReportDto>> RetryAsync(
        int exportPlanId,
        int reportId,
        string ownerUserId,
        CancellationToken cancellationToken = default);

    Task<bool> ProcessNextPendingAsync(CancellationToken cancellationToken = default);
}

public interface IDiseaseVisionAnalyzer
{
    Task<DiseaseVisionResult> AnalyzeAsync(
        DiseaseVisionRequest request,
        CancellationToken cancellationToken = default);
}

public enum DiseaseOperationStatus
{
    Succeeded,
    NotFound,
    Invalid,
    Conflict
}

public sealed record DiseaseOperationResult<T>(
    DiseaseOperationStatus Status,
    T? Value = default,
    string? ErrorCode = null,
    string? ErrorMessage = null)
{
    public static DiseaseOperationResult<T> Success(T value) =>
        new(DiseaseOperationStatus.Succeeded, value);

    public static DiseaseOperationResult<T> NotFound() =>
        new(DiseaseOperationStatus.NotFound);

    public static DiseaseOperationResult<T> Invalid(string code, string message) =>
        new(DiseaseOperationStatus.Invalid, default, code, message);

    public static DiseaseOperationResult<T> Conflict(string code, string message) =>
        new(DiseaseOperationStatus.Conflict, default, code, message);
}
