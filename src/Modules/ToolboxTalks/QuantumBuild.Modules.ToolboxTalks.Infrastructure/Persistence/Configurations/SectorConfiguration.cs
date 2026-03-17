using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using QuantumBuild.Modules.ToolboxTalks.Domain.Entities;

namespace QuantumBuild.Modules.ToolboxTalks.Infrastructure.Persistence.Configurations;

public class SectorConfiguration : IEntityTypeConfiguration<Sector>
{
    public void Configure(EntityTypeBuilder<Sector> builder)
    {
        builder.ToTable("Sectors", "toolbox_talks");
        builder.HasKey(e => e.Id);

        builder.Property(e => e.Key)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(e => e.Name)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(e => e.Icon)
            .HasMaxLength(10);

        builder.Property(e => e.DisplayOrder)
            .IsRequired();

        builder.Property(e => e.IsActive)
            .IsRequired()
            .HasDefaultValue(true);

        // Audit fields
        builder.Property(e => e.CreatedAt)
            .IsRequired();

        builder.Property(e => e.CreatedBy)
            .IsRequired()
            .HasMaxLength(256);

        builder.Property(e => e.UpdatedAt);

        builder.Property(e => e.UpdatedBy)
            .HasMaxLength(256);

        builder.Property(e => e.IsDeleted)
            .IsRequired()
            .HasDefaultValue(false);

        // Relationships
        builder.HasMany(e => e.TenantSectors)
            .WithOne(ts => ts.Sector)
            .HasForeignKey(ts => ts.SectorId)
            .OnDelete(DeleteBehavior.Restrict);

        // Indexes
        builder.HasIndex(e => e.Key)
            .IsUnique()
            .HasDatabaseName("ix_sectors_key");

        // Query filter for soft delete
        builder.HasQueryFilter(e => !e.IsDeleted);
    }
}
