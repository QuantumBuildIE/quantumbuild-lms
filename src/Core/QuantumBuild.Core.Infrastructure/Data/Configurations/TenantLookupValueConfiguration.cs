using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using QuantumBuild.Core.Domain.Entities;

namespace QuantumBuild.Core.Infrastructure.Data.Configurations;

public class TenantLookupValueConfiguration : IEntityTypeConfiguration<TenantLookupValue>
{
    public void Configure(EntityTypeBuilder<TenantLookupValue> builder)
    {
        builder.ToTable("TenantLookupValues");

        builder.HasKey(e => e.Id);

        builder.Property(e => e.Code)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(e => e.Name)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(e => e.Metadata)
            .HasColumnType("jsonb");

        builder.HasIndex(e => new { e.TenantId, e.CategoryId, e.Code })
            .IsUnique()
            .HasDatabaseName("IX_TenantLookupValues_TenantId_CategoryId_Code");

        builder.HasOne(e => e.LookupValue)
            .WithMany()
            .HasForeignKey(e => e.LookupValueId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
