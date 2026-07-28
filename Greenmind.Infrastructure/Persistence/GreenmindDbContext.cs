using Greenmind.Domain.Entities;
using Greenmind.Domain.Consts;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Greenmind.Infrastructure.Persistence;

public class GreenmindDbContext(DbContextOptions<GreenmindDbContext> options)
    : IdentityDbContext<ApplicationUser>(options)   
{
    public DbSet<Farm> Farms => Set<Farm>();
    public DbSet<Crop> Crops => Set<Crop>();
    public DbSet<Destination> Destinations => Set<Destination>();
    public DbSet<ExportPlan> ExportPlans => Set<ExportPlan>();
    public DbSet<FarmTask> FarmTasks => Set<FarmTask>();
    public DbSet<TaskType> TaskTypes => Set<TaskType>();
    public DbSet<SoilMeasurement> SoilMeasurements => Set<SoilMeasurement>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

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
            .IsUnique();

        modelBuilder.Entity<Crop>()
            .Property(crop => crop.IsActive)
            .HasDefaultValue(true);

        modelBuilder.Entity<Destination>()
            .HasIndex(destination => destination.Name)
            .IsUnique();

        modelBuilder.Entity<Destination>()
            .Property(destination => destination.IsActive)
            .HasDefaultValue(true);

        modelBuilder.Entity<TaskType>().HasData(
          new TaskType { Id = 1, Name = "Irrigation", Description = "Watering schedule and management", CreatedAt = new DateTime(2026, 1, 1) },
          new TaskType { Id = 2, Name = "Fertilization", Description = "Nutrient application (N/P/K)", CreatedAt = new DateTime(2026, 1, 1) },
          new TaskType { Id = 3, Name = "PestControl", Description = "Pesticide/fungicide application", CreatedAt = new DateTime(2026, 1, 1) },
          new TaskType { Id = 4, Name = "Pruning", Description = "Plant trimming and shaping", CreatedAt = new DateTime(2026, 1, 1) },
          new TaskType { Id = 5, Name = "SoilTest", Description = "Soil sampling and lab analysis", CreatedAt = new DateTime(2026, 1, 1) },
          new TaskType { Id = 6, Name = "Harvest", Description = "Crop harvesting activities", CreatedAt = new DateTime(2026, 1, 1) },
          new TaskType { Id = 7, Name = "ExportPrep", Description = "Packaging, labeling, export documentation", CreatedAt = new DateTime(2026, 1, 1) },
          new TaskType { Id = 8, Name = "QualityCheck", Description = "Quality/compliance inspection before export", CreatedAt = new DateTime(2026, 1, 1) }
      );

        modelBuilder.Entity<Crop>().HasData(
            new Crop { Id = 1, Name = "Strawberry", Description = "Fresh strawberry crop for export planning", CreatedAt = new DateTime(2026, 1, 1) },
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
            .HasIndex(t => new { t.ExportPlanId, t.TaskTypeId, t.PlannedDate })
            .IsUnique();


        modelBuilder.Entity<FarmTask>()
            .Property(t => t.Status)
            .HasConversion<string>();


        modelBuilder.Entity<FarmTask>()
            .Property(t => t.Priority)
            .HasConversion<string>();
    }
}
