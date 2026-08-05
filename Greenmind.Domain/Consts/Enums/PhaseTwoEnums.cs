namespace Greenmind.Domain.Consts.Enums;

public enum ChatScope
{
    ExportPlan = 1,
    Farm = 2,
    Diagnosis = 3
}

public enum ChatMessageRole
{
    User = 1,
    Assistant = 2,
    System = 3
}

public enum ChatMessageStatus
{
    Sending = 1,
    Completed = 2,
    Failed = 3
}

public enum SupportMessageRole
{
    Customer = 1,
    Admin = 2
}

public enum TaskChangeProposalStatus
{
    PendingReview = 1,
    Applied = 2,
    Rejected = 3,
    Expired = 4,
    Failed = 5
}

public enum TaskChangeOperation
{
    Add = 1,
    Modify = 2,
    Cancel = 3
}

public enum DiseaseAnalysisStatus
{
    Waiting = 1,
    Analyzing = 2,
    Completed = 3,
    Failed = 4,
    Draft = 5,
    Ready = 6,
    Cancelled = 7
}

public enum DiseaseSeverity
{
    Low = 1,
    Medium = 2,
    High = 3,
    Critical = 4
}

public enum ExpensePaymentMethod
{
    Cash = 1,
    BankTransfer = 2,
    Card = 3,
    DigitalWallet = 4
}

public enum ExpenseAttachmentType
{
    Receipt = 1,
    Invoice = 2,
    Other = 3
}
