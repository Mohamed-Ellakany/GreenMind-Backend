namespace Greenmind.Application.Contracts.Api;

public sealed record ApiError(
    string Code,
    string Message,
    int StatusCode,
    IReadOnlyDictionary<string, string[]>? ValidationErrors = null);
