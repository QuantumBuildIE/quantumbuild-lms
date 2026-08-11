using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using QuantumBuild.Core.Domain.Entities;

namespace QuantumBuild.Core.Infrastructure.Data.Configurations;

public class DepartmentConfiguration : IEntityTypeConfiguration<Department>
{
    public void Configure(EntityTypeBuilder<Department> builder)
    {
        builder.HasKey(e => e.Id);

        builder.Property(e => e.Name)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(e => e.Code)
            .HasMaxLength(20);

        builder.HasIndex(e => new { e.TenantId, e.Name })
            .IsUnique()
            .HasDatabaseName("IX_Departments_TenantId_Name");
    }
}
