namespace Greenmind.Application.Contracts.Api;

public sealed record ApiResponse<TData>(
    bool Succeeded,
    TData? Data,
    string? Message,
    ApiError? Error,
    string? TraceId,
    DateTimeOffset Timestamp) : IApiResponse;
