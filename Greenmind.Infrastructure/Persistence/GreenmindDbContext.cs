using Greenmind.Domain.Entities;
using Greenmind.Domain.Consts.Enums;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Greenmind.Infrastructure.Persistence;

public class GreenmindDbContext(DbContextOptions<GreenmindDbContext> options)
    : IdentityDbContext<ApplicationUser, IdentityRole<int>, int>(options)
{
    public DbSet<Farm> Farms => Set<Farm>();

    public DbSet<Crop> Crops => Set<Crop>();

    public DbSet<Destination> Destinations => Set<Destination>();

    public DbSet<ExportPlan> ExportPlans => Set<ExportPlan>();

    public DbSet<Domain.Entities.Task> Tasks => Set<Domain.Entities.Task>();

    public DbSet<TaskType> TaskTypes => Set<TaskType>();

    public DbSet<SoilMeasurement> SoilMeasurements => Set<SoilMeasurement>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.ApplyConfigurationsFromAssembly(typeof(GreenmindDbContext).Assembly);

        modelBuilder.Entity<Farm>()
            .HasMany(f => f.ExportPlans)
            .WithOne(p => p.Farm)
            .HasForeignKey(p => p.FarmId);

        modelBuilder.Entity<Crop>()
            .HasMany(c => c.ExportPlans)
            .WithOne(p => p.Crop)
            .HasForeignKey(p => p.CropId);

        modelBuilder.Entity<Destination>()
            .HasMany(d => d.ExportPlans)
            .WithOne(p => p.Destination)
            .HasForeignKey(p => p.DestinationId);

        modelBuilder.Entity<ExportPlan>()
            .HasMany(p => p.Tasks)
            .WithOne(t => t.ExportPlan)
            .HasForeignKey(t => t.ExportPlanId);

        modelBuilder.Entity<TaskType>()
            .HasMany(tt => tt.Tasks)
            .WithOne(t => t.TaskType)
            .HasForeignKey(t => t.TaskTypeId);

        modelBuilder.Entity<ExportPlan>()
            .HasMany(p => p.SoilMeasurements)
            .WithOne(s => s.ExportPlan)
            .HasForeignKey(s => s.ExportPlanId);
    }
}
