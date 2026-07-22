using Greenmind.Domain.Entities;
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


        modelBuilder.Entity<FarmTask>()
            .Property(t => t.Status)
            .HasConversion<string>();


        modelBuilder.Entity<FarmTask>()
            .Property(t => t.Priority)
            .HasConversion<string>();
    }
}