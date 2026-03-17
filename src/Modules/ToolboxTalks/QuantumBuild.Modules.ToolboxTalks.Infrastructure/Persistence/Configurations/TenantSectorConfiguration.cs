using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using QuantumBuild.Modules.ToolboxTalks.Domain.Entities;

namespace QuantumBuild.Modules.ToolboxTalks.Infrastructure.Persistence.Configurations;

public class TenantSectorConfiguration : IEntityTypeConfiguration<TenantSector>
{
    public void Configure(EntityTypeBuilder<TenantSector> builder)
    {
        builder.ToTable("TenantSectors", "toolbox_talks");
        builder.HasKey(e => e.Id);

        builder.Property(e => e.SectorId)
            .IsRequired();

        builder.Property(e => e.IsDefault)
            .IsRequired()
            .HasDefaultValue(false);

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

        // FK to Sector (configured via Sector's HasMany)
        // FK to Tenant (cross-module — no navigation property on Tenant)
        builder.HasOne<QuantumBuild.Core.Domain.Entities.Tenant>()
            .WithMany()
            .HasForeignKey(e => e.TenantId)
            .OnDelete(DeleteBehavior.Restrict);

        // Indexes
        builder.HasIndex(e => new { e.TenantId, e.SectorId })
            .IsUnique()
            .HasDatabaseName("ix_tenant_sectors_tenant_sector");

        // Note: query filter is defined in ApplicationDbContext (tenant-scoped)
    }
}
