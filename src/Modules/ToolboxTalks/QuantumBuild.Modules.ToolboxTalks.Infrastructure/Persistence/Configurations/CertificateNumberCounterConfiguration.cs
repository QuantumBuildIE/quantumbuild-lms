using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using QuantumBuild.Modules.ToolboxTalks.Domain.Entities;

namespace QuantumBuild.Modules.ToolboxTalks.Infrastructure.Persistence.Configurations;

/// <summary>
/// Entity Framework configuration for CertificateNumberCounter entity
/// </summary>
public class CertificateNumberCounterConfiguration : IEntityTypeConfiguration<CertificateNumberCounter>
{
    public void Configure(EntityTypeBuilder<CertificateNumberCounter> builder)
    {
        builder.ToTable("CertificateNumberCounters", "toolbox_talks");

        builder.HasKey(c => c.Id);

        builder.Property(c => c.Prefix)
            .IsRequired()
            .HasMaxLength(20);

        builder.Property(c => c.Year)
            .IsRequired();

        builder.Property(c => c.LastNumber)
            .IsRequired();

        builder.Property(c => c.TenantId)
            .IsRequired();

        // Audit fields
        builder.Property(c => c.CreatedAt)
            .IsRequired();

        builder.Property(c => c.CreatedBy)
            .IsRequired()
            .HasMaxLength(256);

        builder.Property(c => c.UpdatedAt);

        builder.Property(c => c.UpdatedBy)
            .HasMaxLength(256);

        builder.Property(c => c.IsDeleted)
            .IsRequired()
            .HasDefaultValue(false);

        // Not filtered - the allocation path targets this exact index via ON CONFLICT and rows
        // are never soft-deleted, so a partial/filtered index would only add risk of mismatch.
        builder.HasIndex(c => new { c.TenantId, c.Prefix, c.Year })
            .IsUnique()
            .HasDatabaseName("ix_certificate_number_counters_tenant_prefix_year");
    }
}
