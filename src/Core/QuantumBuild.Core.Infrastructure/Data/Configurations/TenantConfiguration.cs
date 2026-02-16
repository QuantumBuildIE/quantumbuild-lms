using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using QuantumBuild.Core.Domain.Entities;

namespace QuantumBuild.Core.Infrastructure.Data.Configurations;

public class TenantConfiguration : IEntityTypeConfiguration<Tenant>
{
    public void Configure(EntityTypeBuilder<Tenant> builder)
    {
        builder.ToTable("Tenants");

        builder.HasKey(e => e.Id);

        builder.Property(e => e.Name)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(e => e.Code)
            .HasMaxLength(50);

        builder.HasIndex(e => e.Name)
            .IsUnique()
            .HasDatabaseName("IX_Tenants_Name");

        builder.HasIndex(e => e.Code)
            .IsUnique()
            .HasFilter("\"Code\" IS NOT NULL")
            .HasDatabaseName("IX_Tenants_Code");
    }
}
