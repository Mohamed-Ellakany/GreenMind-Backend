using Greenmind.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Greenmind.Infrastructure.Persistence.Configurations;

public sealed class ActiveSubstanceConfiguration : IEntityTypeConfiguration<ActiveSubstance>
{
    public void Configure(EntityTypeBuilder<ActiveSubstance> builder)
    {
        builder.ToTable("ActiveSubstances");

        builder.Property(item => item.ApprovalStatus)
            .HasConversion<byte>()
            .HasColumnType("tinyint");

        builder.Property(item => item.IsDeleted).HasDefaultValue(false);

        builder.HasIndex(item => new { item.RegionName, item.ExternalId })
            .IsUnique()
            .HasFilter("[IsDeleted] = 0");

        builder.HasIndex(item => new { item.RegionName, item.Name });
        builder.HasIndex(item => new { item.RegionName, item.CasNumber });
    }
}
