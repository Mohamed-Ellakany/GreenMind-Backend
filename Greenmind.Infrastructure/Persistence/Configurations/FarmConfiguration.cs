using Greenmind.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Greenmind.Infrastructure.Persistence.Configurations
{
    public class FarmConfiguration : IEntityTypeConfiguration<Farm>
    {
        public void Configure(EntityTypeBuilder<Farm> builder)
        {
            builder.Property(f => f.Name)
                .IsRequired()
                .HasMaxLength(200);

            builder.Property(f => f.Status)
                .IsRequired()
                .HasConversion<string>()
                .HasMaxLength(50);

            builder.HasOne(f => f.Owner)
                .WithOne(u => u.Farm)
                .HasForeignKey<Farm>(f => f.OwnerUserId)
                .OnDelete(DeleteBehavior.Restrict);
        }
    }
}
