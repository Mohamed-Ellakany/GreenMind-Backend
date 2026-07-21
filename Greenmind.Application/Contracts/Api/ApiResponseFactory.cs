namespace Greenmind.Application.Contracts.Api;

public static class ApiResponseFactory
{
    private const int BadRequest = 400;
    private const int Unauthorized = 401;
    private const int Forbidden = 403;
    private const int NotFound = 404;
    private const int Conflict = 409;
    private const int InternalServerError = 500;

    private const string DefaultSuccessMessage = "Request completed successfully.";
    private const string DefaultErrorMessage = "The request could not be completed.";
    private const string DefaultExceptionMessage = "An unexpected error occurred.";

    public static ApiResponse<TData> Success<TData>(
        TData? data,
        string? message = null,
        string? traceId = null)
    {
        return new ApiResponse<TData>(
            Succeeded: true,
            Data: data,
            Message: message ?? DefaultSuccessMessage,
            Error: null,
            TraceId: traceId,
            Timestamp: DateTimeOffset.UtcNow);
    }

    public static ApiResponse<TData> Failure<TData>(
        int statusCode,
        string code,
        string message,
        string? traceId = null,
        IReadOnlyDictionary<string, string[]>? validationErrors = null)
    {
        return new ApiResponse<TData>(
            Succeeded: false,
            Data: default,
            Message: null,
            Error: new ApiError(code, message, statusCode, validationErrors),
            TraceId: traceId,
            Timestamp: DateTimeOffset.UtcNow);
    }

    public static ApiResponse<TData> ValidationFailure<TData>(
        IReadOnlyDictionary<string, string[]> validationErrors,
        string? traceId = null)
    {
        return Failure<TData>(
            BadRequest,
            ApiErrorCodes.ValidationFailed,
            "Validation failed.",
            traceId,
            validationErrors);
    }

    public static ApiResponse<TData> FromStatusCode<TData>(
        int statusCode,
        string? traceId = null,
        string? message = null)
    {
        return statusCode switch
        {
            BadRequest => Failure<TData>(
                statusCode,
                ApiErrorCodes.BadRequest,
                message ?? DefaultErrorMessage,
                traceId),
            Unauthorized => Failure<TData>(
                statusCode,
                ApiErrorCodes.Unauthorized,
                message ?? "Authentication is required.",
                traceId),
            Forbidden => Failure<TData>(
                statusCode,
                ApiErrorCodes.Forbidden,
                message ?? "You are not allowed to perform this action.",
                traceId),
            NotFound => Failure<TData>(
                statusCode,
                ApiErrorCodes.NotFound,
                message ?? "The requested resource was not found.",
                traceId),
            Conflict => Failure<TData>(
                statusCode,
                ApiErrorCodes.Conflict,
                message ?? "The request conflicts with the current resource state.",
                traceId),
            _ => Failure<TData>(
                statusCode,
                statusCode >= InternalServerError
                    ? ApiErrorCodes.UnhandledException
                    : ApiErrorCodes.BadRequest,
                message ?? DefaultErrorMessage,
                traceId)
        };
    }

    public static ApiResponse<TData> FromException<TData>(
        Exception _,
        string? traceId = null)
    {
        return Failure<TData>(
            InternalServerError,
            ApiErrorCodes.UnhandledException,
            DefaultExceptionMessage,
            traceId);
    }
}
