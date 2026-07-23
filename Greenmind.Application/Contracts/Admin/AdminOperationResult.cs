namespace Greenmind.Application.Contracts.Admin;

public enum AdminOperationStatus
{
    Succeeded,
    NotFound,
    Conflict
}

public sealed record AdminOperationResult<T>(
    AdminOperationStatus Status,
    T? Value,
    string? ErrorMessage = null)
{
    public static AdminOperationResult<T> Success(T value) =>
        new(AdminOperationStatus.Succeeded, value);

    public static AdminOperationResult<T> NotFound(string message) =>
        new(AdminOperationStatus.NotFound, default, message);

    public static AdminOperationResult<T> Conflict(string message) =>
        new(AdminOperationStatus.Conflict, default, message);
}
