using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using QuantumBuild.Modules.ToolboxTalks.Domain.Entities;

namespace QuantumBuild.Modules.ToolboxTalks.Infrastructure.Persistence.Configurations;

public class TenantStandardSubscriptionConfiguration : IEntityTypeConfiguration<TenantStandardSubscription>
{
    public void Configure(EntityTypeBuilder<TenantStandardSubscription> builder)
    {
        builder.ToTable("TenantStandardSubscriptions", "toolbox_talks");
        builder.HasKey(e => e.Id);

        builder.Property(e => e.RegulatoryBodyId)
            .IsRequired();

        builder.Property(e => e.TenantId)
            .IsRequired();

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

        // FK to RegulatoryBody
        builder.HasOne(e => e.RegulatoryBody)
            .WithMany()
            .HasForeignKey(e => e.RegulatoryBodyId)
            .OnDelete(DeleteBehavior.Restrict);

        // Cross-module FK to Tenant (no navigation property on Tenant side)
        builder.HasOne<QuantumBuild.Core.Domain.Entities.Tenant>()
            .WithMany()
            .HasForeignKey(e => e.TenantId)
            .OnDelete(DeleteBehavior.Restrict);

        // Indexes — composite unique to prevent duplicate subscriptions
        builder.HasIndex(e => new { e.TenantId, e.RegulatoryBodyId })
            .IsUnique()
            .HasDatabaseName("ix_tenant_standard_subscriptions_tenant_body");

    }
}
