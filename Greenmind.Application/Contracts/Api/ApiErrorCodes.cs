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
    public const string ChatSessionNotFound = "chat_session_not_found";
    public const string ProposalExpired = "proposal_expired";
    public const string ProposalStale = "proposal_stale";
    public const string TaskNotEligible = "task_not_eligible";
    public const string DiseaseImageInvalid = "disease_image_invalid";
    public const string DiseaseAnalysisFailed = "disease_analysis_failed";
    public const string BudgetLinesExceedTotal = "budget_lines_exceed_total";
    public const string ExpenseCategoryInvalid = "expense_category_invalid";
    public const string ExpenseTaskInvalid = "expense_task_invalid";
    public const string AttachmentInvalid = "attachment_invalid";
}
