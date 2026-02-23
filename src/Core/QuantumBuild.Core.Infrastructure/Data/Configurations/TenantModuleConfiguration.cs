using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using QuantumBuild.Core.Domain.Entities;

namespace QuantumBuild.Core.Infrastructure.Data.Configurations;

public class TenantModuleConfiguration : IEntityTypeConfiguration<TenantModule>
{
    public void Configure(EntityTypeBuilder<TenantModule> builder)
    {
        builder.HasKey(e => e.Id);

        builder.Property(e => e.ModuleName)
            .HasMaxLength(50)
            .IsRequired();

        builder.HasIndex(e => new { e.TenantId, e.ModuleName })
            .IsUnique()
            .HasFilter("\"IsDeleted\" = false")
            .HasDatabaseName("IX_TenantModules_TenantId_ModuleName");
    }
}
