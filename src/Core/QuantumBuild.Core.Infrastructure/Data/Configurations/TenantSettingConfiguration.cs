using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using QuantumBuild.Core.Domain.Entities;

namespace QuantumBuild.Core.Infrastructure.Data.Configurations;

public class TenantSettingConfiguration : IEntityTypeConfiguration<TenantSetting>
{
    public void Configure(EntityTypeBuilder<TenantSetting> builder)
    {
        builder.ToTable("TenantSettings");

        builder.HasKey(e => e.Id);

        builder.Property(e => e.TenantId).IsRequired();

        builder.Property(e => e.Module)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(e => e.Key)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(e => e.Value)
            .HasMaxLength(2000)
            .IsRequired();

        // Audit fields
        builder.Property(e => e.CreatedAt).IsRequired();
        builder.Property(e => e.CreatedBy).IsRequired().HasMaxLength(256);
        builder.Property(e => e.UpdatedAt);
        builder.Property(e => e.UpdatedBy).HasMaxLength(256);
        builder.Property(e => e.IsDeleted).IsRequired().HasDefaultValue(false);

        // Unique index - one setting per tenant+module+key combination
        builder.HasIndex(e => new { e.TenantId, e.Module, e.Key })
            .IsUnique()
            .HasDatabaseName("IX_TenantSettings_Tenant_Module_Key");

        // Index for fast lookups by tenant
        builder.HasIndex(e => e.TenantId)
            .HasDatabaseName("IX_TenantSettings_TenantId");

        // Query filter for soft delete only (not tenant-scoped in the filter)
        builder.HasQueryFilter(e => !e.IsDeleted);
    }
}
