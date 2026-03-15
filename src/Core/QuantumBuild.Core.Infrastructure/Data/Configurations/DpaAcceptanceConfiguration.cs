using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using QuantumBuild.Core.Domain.Entities;

namespace QuantumBuild.Core.Infrastructure.Data.Configurations;

public class DpaAcceptanceConfiguration : IEntityTypeConfiguration<DpaAcceptance>
{
    public void Configure(EntityTypeBuilder<DpaAcceptance> builder)
    {
        builder.HasKey(e => e.Id);

        builder.Property(e => e.OrganisationLegalName).HasMaxLength(500).IsRequired();
        builder.Property(e => e.SignatoryFullName).HasMaxLength(300).IsRequired();
        builder.Property(e => e.SignatoryRole).HasMaxLength(200).IsRequired();
        builder.Property(e => e.CompanyRegistrationNo).HasMaxLength(100);
        builder.Property(e => e.Country).HasMaxLength(100).IsRequired();
        builder.Property(e => e.IpAddress).HasMaxLength(100).IsRequired();
        builder.Property(e => e.DpaVersion).HasMaxLength(20).IsRequired();

        builder.HasIndex(e => new { e.TenantId, e.DpaVersion })
            .HasDatabaseName("IX_DpaAcceptances_TenantId_DpaVersion");

        builder.HasOne(e => e.Tenant)
            .WithMany()
            .HasForeignKey(e => e.TenantId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(e => e.AcceptedByUser)
            .WithMany()
            .HasForeignKey(e => e.AcceptedByUserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
