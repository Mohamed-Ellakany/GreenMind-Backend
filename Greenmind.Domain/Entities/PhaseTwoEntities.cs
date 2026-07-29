using Greenmind.Domain.Consts.Enums;
using System.ComponentModel.DataAnnotations;

namespace Greenmind.Domain.Entities;

public sealed class ChatSession : BaseEntity
{
    public int FarmId { get; set; }
    public int? ExportPlanId { get; set; }
    public ChatScope Scope { get; set; }

    [MaxLength(200)]
    public required string Title { get; set; }

    public string? Summary { get; set; }
    public int? SummaryThroughMessageId { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime? UpdatedAtUtc { get; set; }

    public Farm Farm { get; set; } = null!;
    public ExportPlan? ExportPlan { get; set; }
    public ChatMessage? SummaryThroughMessage { get; set; }
    public ICollection<ChatMessage> Messages { get; set; } = new List<ChatMessage>();
}

public sealed class ChatMessage : BaseEntity
{
    public int ChatSessionId { get; set; }
    public ChatMessageRole Role { get; set; }
    public required string Content { get; set; }
    public ChatMessageStatus Status { get; set; }

    [MaxLength(100)]
    public string? IdempotencyKey { get; set; }

    public string? ContextReferencesJson { get; set; }

    [MaxLength(100)]
    public string? ModelName { get; set; }

    public int? InputTokens { get; set; }
    public int? OutputTokens { get; set; }

    public ChatSession ChatSession { get; set; } = null!;
    public ICollection<ChatAttachment> Attachments { get; set; } = new List<ChatAttachment>();
    public ICollection<TaskChangeProposal> Proposals { get; set; } = new List<TaskChangeProposal>();
}

public sealed class ChatAttachment : BaseEntity
{
    public int ChatMessageId { get; set; }

    [MaxLength(255)]
    public required string OriginalFileName { get; set; }

    [MaxLength(500)]
    public required string StorageKey { get; set; }

    [MaxLength(100)]
    public required string ContentType { get; set; }

    public long FileSize { get; set; }

    [MaxLength(128)]
    public required string Checksum { get; set; }

    public ChatMessage ChatMessage { get; set; } = null!;
}

public sealed class TaskChangeProposal : BaseEntity
{
    public int ExportPlanId { get; set; }
    public int? SourceChatMessageId { get; set; }
    public int? SourceDiseaseReportId { get; set; }
    public TaskChangeProposalStatus Status { get; set; } = TaskChangeProposalStatus.PendingReview;
    public DateTime ExpiresAtUtc { get; set; }
    public DateTime? ReviewedAtUtc { get; set; }

    [MaxLength(450)]
    public string? ReviewedByUserId { get; set; }

    [MaxLength(500)]
    public string? FailureMessage { get; set; }

    public ExportPlan ExportPlan { get; set; } = null!;
    public ChatMessage? SourceChatMessage { get; set; }
    public DiseaseReport? SourceDiseaseReport { get; set; }
    public ApplicationUser? ReviewedByUser { get; set; }
    public ICollection<TaskChangeProposalItem> Items { get; set; } = new List<TaskChangeProposalItem>();
}

public sealed class TaskChangeProposalItem : BaseEntity
{
    public int TaskChangeProposalId { get; set; }
    public TaskChangeOperation Operation { get; set; }
    public int? FarmTaskId { get; set; }
    public string? OriginalValuesJson { get; set; }
    public required string ProposedValuesJson { get; set; }

    [MaxLength(1000)]
    public required string Reason { get; set; }

    public TaskChangeProposal TaskChangeProposal { get; set; } = null!;
    public FarmTask? FarmTask { get; set; }
}

public sealed class DiseaseReport : BaseEntity
{
    public int ExportPlanId { get; set; }
    public DateOnly ObservationDate { get; set; }

    [MaxLength(500)]
    public string? AffectedArea { get; set; }

    [MaxLength(1000)]
    public string? UserNotes { get; set; }

    public DiseaseAnalysisStatus Status { get; set; } = DiseaseAnalysisStatus.Waiting;

    [MaxLength(300)]
    public string? SuspectedDisease { get; set; }

    public decimal? ConfidencePercentage { get; set; }
    public DiseaseSeverity? Severity { get; set; }
    public string? ObservedSymptoms { get; set; }
    public string? RecommendedActions { get; set; }
    public string? ExportImpact { get; set; }

    [MaxLength(1000)]
    public string? AdvisoryWarning { get; set; }

    [MaxLength(100)]
    public string? ModelName { get; set; }

    [MaxLength(50)]
    public string? PromptVersion { get; set; }

    [MaxLength(500)]
    public string? FailureMessage { get; set; }

    public DateTime? CompletedAtUtc { get; set; }

    public ExportPlan ExportPlan { get; set; } = null!;
    public ICollection<DiseaseImage> Images { get; set; } = new List<DiseaseImage>();
    public ICollection<TaskChangeProposal> Proposals { get; set; } = new List<TaskChangeProposal>();
}

public sealed class DiseaseImage : BaseEntity
{
    public int DiseaseReportId { get; set; }

    [MaxLength(255)]
    public required string OriginalFileName { get; set; }

    [MaxLength(500)]
    public required string StorageKey { get; set; }

    [MaxLength(100)]
    public required string ContentType { get; set; }

    public long FileSize { get; set; }

    [MaxLength(128)]
    public required string Checksum { get; set; }

    public DiseaseReport DiseaseReport { get; set; } = null!;
}

public sealed class ExpenseCategory : BaseEntity
{
    public int FarmId { get; set; }

    [MaxLength(100)]
    public required string Name { get; set; }

    public bool IsDefault { get; set; }

    public Farm Farm { get; set; } = null!;
    public ICollection<ExportPlanBudgetLine> BudgetLines { get; set; } = new List<ExportPlanBudgetLine>();
    public ICollection<Expense> Expenses { get; set; } = new List<Expense>();
}

public sealed class ExportPlanBudget : BaseEntity
{
    public int ExportPlanId { get; set; }
    public decimal? TotalBudget { get; set; }

    [MaxLength(1000)]
    public string? Notes { get; set; }

    public DateTime UpdatedAtUtc { get; set; }

    public ExportPlan ExportPlan { get; set; } = null!;
    public ICollection<ExportPlanBudgetLine> Lines { get; set; } = new List<ExportPlanBudgetLine>();

    public decimal? EffectivePlannedBudget =>
        TotalBudget ?? (Lines.Count == 0 ? null : Lines.Sum(line => line.PlannedAmount));
}

public sealed class ExportPlanBudgetLine : BaseEntity
{
    public int BudgetId { get; set; }
    public int ExpenseCategoryId { get; set; }
    public decimal PlannedAmount { get; set; }

    public ExportPlanBudget Budget { get; set; } = null!;
    public ExpenseCategory ExpenseCategory { get; set; } = null!;
}

public sealed class Expense : BaseEntity
{
    public int ExportPlanId { get; set; }
    public int ExpenseCategoryId { get; set; }
    public int? TaskId { get; set; }
    public DateOnly ExpenseDate { get; set; }

    [MaxLength(500)]
    public required string Description { get; set; }

    [MaxLength(300)]
    public string? Supplier { get; set; }

    public decimal Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal Amount { get; private set; }
    public ExpensePaymentMethod PaymentMethod { get; set; }

    [MaxLength(1000)]
    public string? Notes { get; set; }

    [MaxLength(450)]
    public required string CreatedByUserId { get; set; }

    [MaxLength(450)]
    public string? UpdatedByUserId { get; set; }

    public DateTime? UpdatedAtUtc { get; set; }
    public bool IsDeleted { get; set; }

    [MaxLength(450)]
    public string? DeletedByUserId { get; set; }

    public DateTime? DeletedAtUtc { get; set; }

    public ExportPlan ExportPlan { get; set; } = null!;
    public ExpenseCategory ExpenseCategory { get; set; } = null!;
    public FarmTask? Task { get; set; }
    public ApplicationUser CreatedByUser { get; set; } = null!;
    public ApplicationUser? UpdatedByUser { get; set; }
    public ApplicationUser? DeletedByUser { get; set; }
    public ICollection<ExpenseAttachment> Attachments { get; set; } = new List<ExpenseAttachment>();

    public void RecalculateAmount() =>
        Amount = decimal.Round(Quantity * UnitPrice, 2, MidpointRounding.AwayFromZero);
}

public sealed class ExpenseAttachment : BaseEntity
{
    public int ExpenseId { get; set; }
    public ExpenseAttachmentType AttachmentType { get; set; }

    [MaxLength(255)]
    public required string OriginalFileName { get; set; }

    [MaxLength(500)]
    public required string StorageKey { get; set; }

    [MaxLength(100)]
    public required string ContentType { get; set; }

    public long FileSize { get; set; }

    [MaxLength(128)]
    public required string Checksum { get; set; }

    [MaxLength(450)]
    public required string UploadedByUserId { get; set; }

    public Expense Expense { get; set; } = null!;
    public ApplicationUser UploadedByUser { get; set; } = null!;
}
