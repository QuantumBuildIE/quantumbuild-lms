using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using QuantumBuild.Modules.ToolboxTalks.Domain.Entities;

namespace QuantumBuild.Modules.ToolboxTalks.Infrastructure.Persistence.Configurations;

public class TenantReviewerConfigurationConfiguration : IEntityTypeConfiguration<TenantReviewerConfiguration>
{
    public void Configure(EntityTypeBuilder<TenantReviewerConfiguration> builder)
    {
        builder.ToTable("TenantReviewerConfigurations", "toolbox_talks");
        builder.HasKey(e => e.Id);

        builder.Property(e => e.LanguageCode)
            .HasMaxLength(10);

        builder.Property(e => e.ReviewerEmail)
            .IsRequired()
            .HasMaxLength(256);

        builder.Property(e => e.ReviewerName)
            .HasMaxLength(256);

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

        // FK to Tenant (cross-module — no navigation property on Tenant)
        builder.HasOne<QuantumBuild.Core.Domain.Entities.Tenant>()
            .WithMany()
            .HasForeignKey(e => e.TenantId)
            .OnDelete(DeleteBehavior.Restrict);

        // At most one row per (tenant, language) for a specific language.
        // PostgreSQL treats NULL as distinct in a plain unique index, so this filtered
        // index only applies to non-null language rows.
        builder.HasIndex(e => new { e.TenantId, e.LanguageCode })
            .IsUnique()
            .HasFilter("\"IsDeleted\" = false AND \"LanguageCode\" IS NOT NULL")
            .HasDatabaseName("ix_tenant_reviewer_configurations_tenant_language");

        // At most one "all languages" fallback row (LanguageCode == null) per tenant.
        builder.HasIndex(e => e.TenantId)
            .IsUnique()
            .HasFilter("\"IsDeleted\" = false AND \"LanguageCode\" IS NULL")
            .HasDatabaseName("ix_tenant_reviewer_configurations_tenant_fallback");

        // Note: query filter is defined in ApplicationDbContext (tenant-scoped)
    }
}
