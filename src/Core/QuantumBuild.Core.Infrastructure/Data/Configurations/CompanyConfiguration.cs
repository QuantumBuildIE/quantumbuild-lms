using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using QuantumBuild.Core.Domain.Entities;

namespace QuantumBuild.Core.Infrastructure.Data.Configurations;

public class CompanyConfiguration : IEntityTypeConfiguration<Company>
{
    public void Configure(EntityTypeBuilder<Company> builder)
    {
        builder.HasKey(e => e.Id);

        builder.Property(e => e.CompanyCode)
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(e => e.CompanyName)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(e => e.TradingName)
            .HasMaxLength(200);

        builder.Property(e => e.RegistrationNumber)
            .HasMaxLength(50);

        builder.Property(e => e.VatNumber)
            .HasMaxLength(50);

        builder.Property(e => e.AddressLine1)
            .HasMaxLength(200);

        builder.Property(e => e.AddressLine2)
            .HasMaxLength(200);

        builder.Property(e => e.City)
            .HasMaxLength(100);

        builder.Property(e => e.County)
            .HasMaxLength(100);

        builder.Property(e => e.PostalCode)
            .HasMaxLength(20);

        builder.Property(e => e.Country)
            .HasMaxLength(100);

        builder.Property(e => e.Phone)
            .HasMaxLength(50);

        builder.Property(e => e.Email)
            .HasMaxLength(255);

        builder.Property(e => e.Website)
            .HasMaxLength(255);

        builder.Property(e => e.CompanyType)
            .HasMaxLength(50);

        builder.Property(e => e.Notes)
            .HasMaxLength(2000);

        builder.HasIndex(e => new { e.TenantId, e.CompanyCode })
            .IsUnique()
            .HasDatabaseName("IX_Companies_TenantId_CompanyCode");
    }
}
