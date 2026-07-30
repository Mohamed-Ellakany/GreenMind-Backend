using Greenmind.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Greenmind.Infrastructure.Persistence.Configurations;

public sealed class ProductMrlConfiguration : IEntityTypeConfiguration<ProductMrl>
{
    public void Configure(EntityTypeBuilder<ProductMrl> builder)
    {
        builder.ToTable("ProductMrls");

        builder.Property(item => item.MaximumResidueLevel)
            .HasPrecision(18, 6);

        builder.Property(item => item.IsDeleted).HasDefaultValue(false);

        builder.HasIndex(item => new { item.RegionName, item.ProductType });
        builder.HasIndex(item => new { item.RegionName, item.ProductType, item.ExternalPesticideId });
    }
}
