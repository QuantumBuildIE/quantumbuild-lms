using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using QuantumBuild.Core.Domain.Entities;

namespace QuantumBuild.Core.Infrastructure.Data.Configurations;

public class TenantBrandingConfiguration : IEntityTypeConfiguration<TenantBranding>
{
    public const string TenantIdIndexName = "IX_TenantBrandings_TenantId";

    public void Configure(EntityTypeBuilder<TenantBranding> builder)
    {
        builder.HasKey(e => e.Id);

        builder.Property(e => e.LogoKey).HasMaxLength(500);

        builder.HasIndex(e => e.TenantId)
            .IsUnique()
            .HasFilter("\"IsDeleted\" = false")
            .HasDatabaseName(TenantIdIndexName);

        builder.HasOne(e => e.Tenant)
            .WithMany()
            .HasForeignKey(e => e.TenantId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
