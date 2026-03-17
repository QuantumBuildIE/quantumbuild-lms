using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using QuantumBuild.Modules.ToolboxTalks.Domain.Entities;

namespace QuantumBuild.Modules.ToolboxTalks.Infrastructure.Persistence.Configurations;

public class RegulatoryCriteriaConfiguration : IEntityTypeConfiguration<RegulatoryCriteria>
{
    public void Configure(EntityTypeBuilder<RegulatoryCriteria> builder)
    {
        builder.ToTable("RegulatoryCriteria", "toolbox_talks");
        builder.HasKey(e => e.Id);

        builder.Property(e => e.RegulatoryProfileId)
            .IsRequired();

        // Nullable TenantId: null = system default, Guid = tenant override
        builder.Property(e => e.TenantId);

        builder.Property(e => e.CategoryKey)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(e => e.CriteriaText)
            .IsRequired()
            .HasMaxLength(2000);

        builder.Property(e => e.DisplayOrder)
            .IsRequired();

        builder.Property(e => e.IsActive)
            .IsRequired()
            .HasDefaultValue(true);

        builder.Property(e => e.Source)
            .HasMaxLength(500);

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

        // Relationships (configured via RegulatoryProfile's HasMany)
        // Cross-module FK to Tenant (no navigation property on Tenant side)
        builder.HasOne<QuantumBuild.Core.Domain.Entities.Tenant>()
            .WithMany()
            .HasForeignKey(e => e.TenantId)
            .OnDelete(DeleteBehavior.Restrict);

        // Indexes
        builder.HasIndex(e => new { e.RegulatoryProfileId, e.TenantId, e.CategoryKey, e.DisplayOrder })
            .IsUnique()
            .HasDatabaseName("ix_regulatory_criteria_profile_tenant_category_order");

        builder.HasIndex(e => e.TenantId)
            .HasDatabaseName("ix_regulatory_criteria_tenant");

        // Query filter: !IsDeleted only — tenant filtering handled at service level (SafetyGlossary pattern)
        builder.HasQueryFilter(e => !e.IsDeleted);
    }
}
