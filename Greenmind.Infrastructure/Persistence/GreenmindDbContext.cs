using Greenmind.Domain.Entities;
using Greenmind.Domain.Consts;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Greenmind.Infrastructure.Persistence;

public class GreenmindDbContext(DbContextOptions<GreenmindDbContext> options)
    : IdentityDbContext<ApplicationUser>(options)   
{

    public DbSet<ComplianceDocument> ComplianceDocuments => Set<ComplianceDocument>();
    public DbSet<Farm> Farms => Set<Farm>();
    public DbSet<Crop> Crops => Set<Crop>();
    public DbSet<Destination> Destinations => Set<Destination>();
    public DbSet<ExportPlan> ExportPlans => Set<ExportPlan>();
    public DbSet<FarmTask> FarmTasks => Set<FarmTask>();
    public DbSet<FarmTaskActivity> FarmTaskActivities => Set<FarmTaskActivity>();
    public DbSet<ScheduleRevision> ScheduleRevisions => Set<ScheduleRevision>();
    public DbSet<ScheduleRevisionItem> ScheduleRevisionItems => Set<ScheduleRevisionItem>();
    public DbSet<TaskType> TaskTypes => Set<TaskType>();
    public DbSet<SoilMeasurement> SoilMeasurements => Set<SoilMeasurement>();
    public DbSet<ActiveSubstance> ActiveSubstances => Set<ActiveSubstance>();
    public DbSet<ProductMrl> ProductMrls => Set<ProductMrl>();
    public DbSet<ChatSession> ChatSessions => Set<ChatSession>();
    public DbSet<ChatMessage> ChatMessages => Set<ChatMessage>();
    public DbSet<ChatAttachment> ChatAttachments => Set<ChatAttachment>();
    public DbSet<TaskChangeProposal> TaskChangeProposals => Set<TaskChangeProposal>();
    public DbSet<TaskChangeProposalItem> TaskChangeProposalItems => Set<TaskChangeProposalItem>();
    public DbSet<DiseaseReport> DiseaseReports => Set<DiseaseReport>();
    public DbSet<DiseaseImage> DiseaseImages => Set<DiseaseImage>();
    public DbSet<DiseaseReportSource> DiseaseReportSources => Set<DiseaseReportSource>();
    public DbSet<ExpenseCategory> ExpenseCategories => Set<ExpenseCategory>();
    public DbSet<ExportPlanBudget> ExportPlanBudgets => Set<ExportPlanBudget>();
    public DbSet<ExportPlanBudgetLine> ExportPlanBudgetLines => Set<ExportPlanBudgetLine>();
    public DbSet<Expense> Expenses => Set<Expense>();
    public DbSet<ExpenseAttachment> ExpenseAttachments => Set<ExpenseAttachment>();
    public DbSet<AdminInvitation> AdminInvitations => Set<AdminInvitation>();
    public DbSet<SupportConversation> SupportConversations => Set<SupportConversation>();
    public DbSet<SupportMessage> SupportMessages => Set<SupportMessage>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<AdminInvitation>(entity =>
        {
            entity.HasIndex(item => item.TokenHash).IsUnique();
            entity.HasIndex(item => new { item.Email, item.ExpiresAtUtc });
            entity.HasOne(item => item.CreatedByUser).WithMany(user => user.CreatedAdminInvitations)
                .HasForeignKey(item => item.CreatedByUserId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.ApplyConfigurationsFromAssembly(typeof(GreenmindDbContext).Assembly);

        modelBuilder.Entity<IdentityRole>().HasData(
            new IdentityRole
            {
                Id = "7e8c8cce-78f5-4b44-b6aa-40418b037f57",
                Name = RoleNames.Admin,
                NormalizedName = RoleNames.Admin.ToUpperInvariant(),
                ConcurrencyStamp = "7e8c8cce-78f5-4b44-b6aa-40418b037f57"
            },
            new IdentityRole
            {
                Id = "47ab3c36-a117-47eb-a18c-8545dd22cb01",
                Name = RoleNames.FarmOwner,
                NormalizedName = RoleNames.FarmOwner.ToUpperInvariant(),
                ConcurrencyStamp = "47ab3c36-a117-47eb-a18c-8545dd22cb01"
            });

        modelBuilder.Entity<Crop>()
            .HasIndex(crop => crop.Name)
            .IsUnique()
            .HasFilter("[IsDeleted] = 0");

        modelBuilder.Entity<Crop>()
            .Property(crop => crop.IsActive)
            .HasDefaultValue(true);

        modelBuilder.Entity<Crop>()
            .Property(crop => crop.IsDeleted)
            .HasDefaultValue(false);

        modelBuilder.Entity<Destination>()
            .HasIndex(destination => destination.Name)
            .IsUnique()
            .HasFilter("[IsDeleted] = 0");

        modelBuilder.Entity<Destination>()
            .Property(destination => destination.IsActive)
            .HasDefaultValue(true);

        modelBuilder.Entity<Destination>()
            .Property(destination => destination.IsDeleted)
            .HasDefaultValue(false);

        modelBuilder.Entity<TaskType>().HasData(
          new TaskType { Id = 1, Name = "Irrigation", Description = "Watering schedule and management", CreatedAt = new DateTime(2026, 1, 1) },
          new TaskType { Id = 2, Name = "Fertilization", Description = "Nutrient application (N/P/K)", CreatedAt = new DateTime(2026, 1, 1) },
          new TaskType { Id = 3, Name = "PestControl", Description = "Pesticide/fungicide application", CreatedAt = new DateTime(2026, 1, 1) },
          new TaskType { Id = 4, Name = "Pruning", Description = "Plant trimming and shaping", CreatedAt = new DateTime(2026, 1, 1) },
          new TaskType { Id = 5, Name = "SoilTest", Description = "Soil sampling and lab analysis", CreatedAt = new DateTime(2026, 1, 1) },
          new TaskType { Id = 6, Name = "Harvest", Description = "Crop harvesting activities", CreatedAt = new DateTime(2026, 1, 1) },
          new TaskType { Id = 7, Name = "ExportPrep", Description = "Packaging, labeling, export documentation", CreatedAt = new DateTime(2026, 1, 1) },
          new TaskType { Id = 8, Name = "QualityCheck", Description = "Quality/compliance inspection before export", CreatedAt = new DateTime(2026, 1, 1) },
          new TaskType { Id = 9, Name = "Other", Description = "Other farm activity recorded by the user", CreatedAt = new DateTime(2026, 1, 1) }
      );

        modelBuilder.Entity<Crop>().HasData(
            new Crop { Id = CropCatalog.Strawberry.Id, Name = CropCatalog.Strawberry.DefaultDisplayName, Description = "Fresh strawberry crop for export planning", CreatedAt = new DateTime(2026, 1, 1) },
            new Crop { Id = 2, Name = "Grapes", Description = "Table grapes crop for export planning", CreatedAt = new DateTime(2026, 1, 1) },
            new Crop { Id = 3, Name = "Citrus", Description = "Citrus crop for export planning", CreatedAt = new DateTime(2026, 1, 1) },
            new Crop { Id = 4, Name = "Potato", Description = "Potato crop for export planning", CreatedAt = new DateTime(2026, 1, 1) },
            new Crop { Id = 5, Name = "Onion", Description = "Onion crop for export planning", CreatedAt = new DateTime(2026, 1, 1) },
            new Crop { Id = 6, Name = "Green Beans", Description = "Green beans crop for export planning", CreatedAt = new DateTime(2026, 1, 1) }
        );

        modelBuilder.Entity<Destination>().HasData(
            new Destination { Id = 1, Name = "Germany", Description = "EU destination market", CreatedAt = new DateTime(2026, 1, 1) },
            new Destination { Id = 2, Name = "Netherlands", Description = "EU destination market", CreatedAt = new DateTime(2026, 1, 1) },
            new Destination { Id = 3, Name = "France", Description = "EU destination market", CreatedAt = new DateTime(2026, 1, 1) },
            new Destination { Id = 4, Name = "Italy", Description = "EU destination market", CreatedAt = new DateTime(2026, 1, 1) },
            new Destination { Id = 5, Name = "Spain", Description = "EU destination market", CreatedAt = new DateTime(2026, 1, 1) },
            new Destination { Id = 6, Name = "Belgium", Description = "EU destination market", CreatedAt = new DateTime(2026, 1, 1) },
            new Destination { Id = 7, Name = "European Union", Description = "European Union export market", CreatedAt = new DateTime(2026, 1, 1) },
            new Destination { Id = 8, Name = "Saudi Arabia", Description = "Saudi Arabian export market", CreatedAt = new DateTime(2026, 1, 1) }
        );


        modelBuilder.Entity<Farm>()
            .HasMany(f => f.ExportPlans)
            .WithOne(p => p.Farm)
            .HasForeignKey(p => p.FarmId);

		modelBuilder.Entity<Farm>()
            .HasOne(f => f.Owner)
            .WithOne(u => u.Farm)
            .HasForeignKey<Farm>(f=>f.OwnerUserId)
            .OnDelete(DeleteBehavior.Restrict);

		modelBuilder.Entity<Crop>()
            .HasMany(c => c.ExportPlans)
            .WithOne(p => p.Crop)
            .HasForeignKey(p => p.CropId);

        modelBuilder.Entity<Destination>()
            .HasMany(d => d.ExportPlans)
            .WithOne(p => p.Destination)
            .HasForeignKey(p => p.DestinationId);

        modelBuilder.Entity<ExportPlan>()
            .HasOne(p => p.Farm)
            .WithMany(f => f.ExportPlans)
            .HasForeignKey(p => p.FarmId)
            .OnDelete(DeleteBehavior.Restrict);



        modelBuilder.Entity<ExportPlan>()
            .HasOne(p => p.Crop)
            .WithMany(c => c.ExportPlans)
            .HasForeignKey(p => p.CropId)
            .OnDelete(DeleteBehavior.Restrict);



        modelBuilder.Entity<ExportPlan>()
            .HasOne(p => p.Destination)
            .WithMany(d => d.ExportPlans)
            .HasForeignKey(p => p.DestinationId)
            .OnDelete(DeleteBehavior.Restrict);


        modelBuilder.Entity<FarmTask>()
            .HasOne(t => t.ExportPlan)
            .WithMany(p => p.FarmTasks)
            .HasForeignKey(t => t.ExportPlanId)
            .OnDelete(DeleteBehavior.Cascade);


        modelBuilder.Entity<FarmTask>()
            .HasOne(t => t.TaskType)
            .WithMany(tt => tt.FarmTasks)
            .HasForeignKey(t => t.TaskTypeId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<FarmTask>()
            .HasMany(t => t.Activities)
            .WithOne(activity => activity.FarmTask)
            .HasForeignKey(activity => activity.FarmTaskId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<SoilMeasurement>()
            .HasOne(s => s.ExportPlan)
            .WithMany(p => p.SoilMeasurements)
            .HasForeignKey(s => s.ExportPlanId)
            .OnDelete(DeleteBehavior.Cascade);


        modelBuilder.Entity<ExportPlan>()
            .Property(p => p.Status)
            .HasConversion<string>();

        modelBuilder.Entity<ExportPlan>()
            .Property(p => p.PlanningStatus)
            .HasConversion<string>();

        modelBuilder.Entity<FarmTask>()
            .HasIndex(t => new { t.ExportPlanId, t.PlannedDate });


        modelBuilder.Entity<FarmTask>()
            .Property(t => t.Status)
            .HasConversion<string>();


        modelBuilder.Entity<FarmTask>()
            .Property(t => t.Priority)
            .HasConversion<string>();

        modelBuilder.Entity<FarmTask>()
            .Property(t => t.Origin)
            .HasConversion<string>()
            .HasMaxLength(32)
            .HasDefaultValue(Greenmind.Domain.Consts.Enums.TaskOrigin.AIPlanned);

        modelBuilder.Entity<FarmTaskActivity>()
            .Property(activity => activity.PreviousStatus)
            .HasConversion<string>()
            .HasMaxLength(32);

        modelBuilder.Entity<FarmTaskActivity>()
            .Property(activity => activity.NewStatus)
            .HasConversion<string>()
            .HasMaxLength(32);

        modelBuilder.Entity<FarmTaskActivity>()
            .HasIndex(activity => new { activity.FarmTaskId, activity.CreatedAt });

        modelBuilder.Entity<ScheduleRevision>()
            .HasOne(revision => revision.ExportPlan)
            .WithMany(plan => plan.ScheduleRevisions)
            .HasForeignKey(revision => revision.ExportPlanId)
            .OnDelete(DeleteBehavior.Cascade);
        modelBuilder.Entity<ScheduleRevision>()
            .Property(revision => revision.Status)
            .HasConversion<string>()
            .HasMaxLength(32);
        modelBuilder.Entity<ScheduleRevision>()
            .HasIndex(revision => new { revision.ExportPlanId, revision.Status, revision.CreatedAt });
        modelBuilder.Entity<ScheduleRevisionItem>()
            .HasOne(item => item.ScheduleRevision)
            .WithMany(revision => revision.Items)
            .HasForeignKey(item => item.ScheduleRevisionId)
            .OnDelete(DeleteBehavior.Cascade);
        modelBuilder.Entity<ScheduleRevisionItem>()
            .HasOne(item => item.FarmTask)
            .WithMany()
            .HasForeignKey(item => item.FarmTaskId)
            .OnDelete(DeleteBehavior.NoAction);
        modelBuilder.Entity<ScheduleRevisionItem>()
            .Property(item => item.Operation)
            .HasConversion<string>()
            .HasMaxLength(32);

        modelBuilder.Entity<FarmTask>().Property(t => t.Quantity).HasPrecision(18, 3);
        modelBuilder.Entity<FarmTask>().Property(t => t.Unit).HasMaxLength(50).IsRequired();
        modelBuilder.Entity<FarmTask>().ToTable("FarmTasks", table => table.HasCheckConstraint("CK_FarmTasks_Quantity", "[Quantity] > 0"));



        modelBuilder.Entity<ComplianceDocument>(entity =>
        {
            // Store enum as string for readability in the DB (consistent with your other enums)
            entity.Property(d => d.Category).HasConversion<string>();

            // Index the columns the repository actually filters on, since SearchAsync
            // pre-filters by Category/Crop/Destination before doing vector math in memory.
            entity.HasIndex(d => new { d.Category, d.Crop, d.Destination });

            // Embedding JSON can get long (1536 floats ≈ 20-30KB as text) - use unbounded/max length.
            entity.Property(d => d.Embedding).HasColumnType("nvarchar(max)");
            entity.Property(d => d.Content).HasColumnType("nvarchar(max)");
        });
    }
}
