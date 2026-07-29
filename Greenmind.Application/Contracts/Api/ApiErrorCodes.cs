namespace Greenmind.Application.Contracts.Api;

public static class ApiErrorCodes
{
    public const string ValidationFailed = "validation_failed";
    public const string Unauthorized = "unauthorized";
    public const string Forbidden = "forbidden";
    public const string NotFound = "not_found";
    public const string Conflict = "conflict";
    public const string BadRequest = "bad_request";
    public const string UnhandledException = "unhandled_exception";
    public const string AccountPendingApproval = "account_pending_approval";
    public const string AccountInactive = "account_inactive";
}
