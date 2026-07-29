using Greenmind.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Greenmind.Infrastructure.Persistence.Configurations;

public sealed class ChatSessionConfiguration : IEntityTypeConfiguration<ChatSession>
{
    public void Configure(EntityTypeBuilder<ChatSession> builder)
    {
        builder.ToTable("ChatSessions", table => table.HasCheckConstraint(
            "CK_ChatSessions_Scope",
            "([Scope] = 1 AND [ExportPlanId] IS NOT NULL) OR ([Scope] = 2 AND [ExportPlanId] IS NULL)"));

        builder.Property(item => item.Scope).HasConversion<int>();
        builder.Property(item => item.Summary).HasColumnType("nvarchar(max)");
        builder.Property(item => item.IsActive).HasDefaultValue(true);

        builder.HasIndex(item => new { item.FarmId, item.Scope, item.IsActive });
        builder.HasIndex(item => new { item.ExportPlanId, item.CreatedAt });

        builder.HasOne(item => item.Farm)
            .WithMany(farm => farm.ChatSessions)
            .HasForeignKey(item => item.FarmId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(item => item.ExportPlan)
            .WithMany(plan => plan.ChatSessions)
            .HasForeignKey(item => item.ExportPlanId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(item => item.SummaryThroughMessage)
            .WithMany()
            .HasForeignKey(item => item.SummaryThroughMessageId)
            .OnDelete(DeleteBehavior.NoAction);
    }
}

public sealed class ChatMessageConfiguration : IEntityTypeConfiguration<ChatMessage>
{
    public void Configure(EntityTypeBuilder<ChatMessage> builder)
    {
        builder.ToTable("ChatMessages");
        builder.Property(item => item.Role).HasConversion<int>();
        builder.Property(item => item.Status).HasConversion<int>();
        builder.Property(item => item.Content).HasColumnType("nvarchar(max)");
        builder.Property(item => item.ContextReferencesJson).HasColumnType("nvarchar(max)");

        builder.HasIndex(item => new { item.ChatSessionId, item.CreatedAt, item.Id });
        builder.HasIndex(item => new { item.ChatSessionId, item.IdempotencyKey })
            .IsUnique()
            .HasFilter("[IdempotencyKey] IS NOT NULL");

        builder.HasOne(item => item.ChatSession)
            .WithMany(session => session.Messages)
            .HasForeignKey(item => item.ChatSessionId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public sealed class ChatAttachmentConfiguration : IEntityTypeConfiguration<ChatAttachment>
{
    public void Configure(EntityTypeBuilder<ChatAttachment> builder)
    {
        builder.ToTable("ChatAttachments");
        builder.HasIndex(item => item.StorageKey).IsUnique();
        builder.HasOne(item => item.ChatMessage)
            .WithMany(message => message.Attachments)
            .HasForeignKey(item => item.ChatMessageId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public sealed class TaskChangeProposalConfiguration : IEntityTypeConfiguration<TaskChangeProposal>
{
    public void Configure(EntityTypeBuilder<TaskChangeProposal> builder)
    {
        builder.ToTable("TaskChangeProposals", table => table.HasCheckConstraint(
            "CK_TaskChangeProposals_Source",
            "NOT ([SourceChatMessageId] IS NOT NULL AND [SourceDiseaseReportId] IS NOT NULL)"));

        builder.Property(item => item.Status).HasConversion<int>();
        builder.HasIndex(item => new { item.ExportPlanId, item.Status, item.ExpiresAtUtc });

        builder.HasOne(item => item.ExportPlan)
            .WithMany(plan => plan.TaskChangeProposals)
            .HasForeignKey(item => item.ExportPlanId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(item => item.SourceChatMessage)
            .WithMany(message => message.Proposals)
            .HasForeignKey(item => item.SourceChatMessageId)
            .OnDelete(DeleteBehavior.NoAction);

        builder.HasOne(item => item.SourceDiseaseReport)
            .WithMany(report => report.Proposals)
            .HasForeignKey(item => item.SourceDiseaseReportId)
            .OnDelete(DeleteBehavior.NoAction);

        builder.HasOne(item => item.ReviewedByUser)
            .WithMany()
            .HasForeignKey(item => item.ReviewedByUserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class TaskChangeProposalItemConfiguration : IEntityTypeConfiguration<TaskChangeProposalItem>
{
    public void Configure(EntityTypeBuilder<TaskChangeProposalItem> builder)
    {
        builder.ToTable("TaskChangeProposalItems", table => table.HasCheckConstraint(
            "CK_TaskChangeProposalItems_Target",
            "([Operation] = 1 AND [FarmTaskId] IS NULL) OR ([Operation] IN (2, 3) AND [FarmTaskId] IS NOT NULL)"));

        builder.Property(item => item.Operation).HasConversion<int>();
        builder.Property(item => item.OriginalValuesJson).HasColumnType("nvarchar(max)");
        builder.Property(item => item.ProposedValuesJson).HasColumnType("nvarchar(max)");

        builder.HasOne(item => item.TaskChangeProposal)
            .WithMany(proposal => proposal.Items)
            .HasForeignKey(item => item.TaskChangeProposalId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(item => item.FarmTask)
            .WithMany(task => task.ProposalItems)
            .HasForeignKey(item => item.FarmTaskId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class DiseaseReportConfiguration : IEntityTypeConfiguration<DiseaseReport>
{
    public void Configure(EntityTypeBuilder<DiseaseReport> builder)
    {
        builder.ToTable("DiseaseReports", table => table.HasCheckConstraint(
            "CK_DiseaseReports_Confidence",
            "[ConfidencePercentage] IS NULL OR ([ConfidencePercentage] >= 0 AND [ConfidencePercentage] <= 100)"));

        builder.Property(item => item.Status).HasConversion<int>();
        builder.Property(item => item.Severity).HasConversion<int>();
        builder.Property(item => item.ConfidencePercentage).HasPrecision(5, 2);
        builder.Property(item => item.ObservedSymptoms).HasColumnType("nvarchar(max)");
        builder.Property(item => item.RecommendedActions).HasColumnType("nvarchar(max)");
        builder.Property(item => item.ExportImpact).HasColumnType("nvarchar(max)");

        builder.HasIndex(item => new { item.ExportPlanId, item.ObservationDate, item.CreatedAt });
        builder.HasIndex(item => new { item.Status, item.CreatedAt });

        builder.HasOne(item => item.ExportPlan)
            .WithMany(plan => plan.DiseaseReports)
            .HasForeignKey(item => item.ExportPlanId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public sealed class DiseaseImageConfiguration : IEntityTypeConfiguration<DiseaseImage>
{
    public void Configure(EntityTypeBuilder<DiseaseImage> builder)
    {
        builder.ToTable("DiseaseImages");
        builder.HasIndex(item => item.StorageKey).IsUnique();
        builder.HasOne(item => item.DiseaseReport)
            .WithMany(report => report.Images)
            .HasForeignKey(item => item.DiseaseReportId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public sealed class ExpenseCategoryConfiguration : IEntityTypeConfiguration<ExpenseCategory>
{
    public void Configure(EntityTypeBuilder<ExpenseCategory> builder)
    {
        builder.ToTable("ExpenseCategories");
        builder.HasIndex(item => new { item.FarmId, item.Name }).IsUnique();
        builder.HasOne(item => item.Farm)
            .WithMany(farm => farm.ExpenseCategories)
            .HasForeignKey(item => item.FarmId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class ExportPlanBudgetConfiguration : IEntityTypeConfiguration<ExportPlanBudget>
{
    public void Configure(EntityTypeBuilder<ExportPlanBudget> builder)
    {
        builder.ToTable("ExportPlanBudgets", table => table.HasCheckConstraint(
            "CK_ExportPlanBudgets_TotalBudget",
            "[TotalBudget] IS NULL OR [TotalBudget] >= 0"));

        builder.Property(item => item.TotalBudget).HasPrecision(18, 2);
        builder.HasIndex(item => item.ExportPlanId).IsUnique();
        builder.HasOne(item => item.ExportPlan)
            .WithOne(plan => plan.Budget)
            .HasForeignKey<ExportPlanBudget>(item => item.ExportPlanId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public sealed class ExportPlanBudgetLineConfiguration : IEntityTypeConfiguration<ExportPlanBudgetLine>
{
    public void Configure(EntityTypeBuilder<ExportPlanBudgetLine> builder)
    {
        builder.ToTable("ExportPlanBudgetLines", table => table.HasCheckConstraint(
            "CK_ExportPlanBudgetLines_PlannedAmount",
            "[PlannedAmount] >= 0"));

        builder.Property(item => item.PlannedAmount).HasPrecision(18, 2);
        builder.HasIndex(item => new { item.BudgetId, item.ExpenseCategoryId }).IsUnique();

        builder.HasOne(item => item.Budget)
            .WithMany(budget => budget.Lines)
            .HasForeignKey(item => item.BudgetId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(item => item.ExpenseCategory)
            .WithMany(category => category.BudgetLines)
            .HasForeignKey(item => item.ExpenseCategoryId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class ExpenseConfiguration : IEntityTypeConfiguration<Expense>
{
    public void Configure(EntityTypeBuilder<Expense> builder)
    {
        builder.ToTable("Expenses", table =>
        {
            table.HasCheckConstraint("CK_Expenses_Quantity", "[Quantity] > 0");
            table.HasCheckConstraint("CK_Expenses_UnitPrice", "[UnitPrice] >= 0");
            table.HasCheckConstraint("CK_Expenses_Amount", "[Amount] >= 0");
        });

        builder.Property(item => item.Quantity).HasPrecision(18, 3);
        builder.Property(item => item.UnitPrice).HasPrecision(18, 2);
        builder.Property(item => item.Amount).HasPrecision(18, 2);
        builder.Property(item => item.PaymentMethod).HasConversion<int>();
        builder.Property(item => item.IsDeleted).HasDefaultValue(false);

        builder.HasIndex(item => new { item.ExportPlanId, item.IsDeleted, item.ExpenseDate });
        builder.HasIndex(item => new { item.ExpenseCategoryId, item.IsDeleted });
        builder.HasQueryFilter(item => !item.IsDeleted);

        builder.HasOne(item => item.ExportPlan)
            .WithMany(plan => plan.Expenses)
            .HasForeignKey(item => item.ExportPlanId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(item => item.ExpenseCategory)
            .WithMany(category => category.Expenses)
            .HasForeignKey(item => item.ExpenseCategoryId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(item => item.Task)
            .WithMany(task => task.Expenses)
            .HasForeignKey(item => item.TaskId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(item => item.CreatedByUser)
            .WithMany()
            .HasForeignKey(item => item.CreatedByUserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(item => item.UpdatedByUser)
            .WithMany()
            .HasForeignKey(item => item.UpdatedByUserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(item => item.DeletedByUser)
            .WithMany()
            .HasForeignKey(item => item.DeletedByUserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class ExpenseAttachmentConfiguration : IEntityTypeConfiguration<ExpenseAttachment>
{
    public void Configure(EntityTypeBuilder<ExpenseAttachment> builder)
    {
        builder.ToTable("ExpenseAttachments");
        builder.Property(item => item.AttachmentType).HasConversion<int>();
        builder.HasIndex(item => item.StorageKey).IsUnique();
        builder.HasQueryFilter(item => !item.Expense.IsDeleted);

        builder.HasOne(item => item.Expense)
            .WithMany(expense => expense.Attachments)
            .HasForeignKey(item => item.ExpenseId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(item => item.UploadedByUser)
            .WithMany()
            .HasForeignKey(item => item.UploadedByUserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
