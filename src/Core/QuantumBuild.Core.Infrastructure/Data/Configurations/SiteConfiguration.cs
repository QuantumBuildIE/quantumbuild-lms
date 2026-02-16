using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using QuantumBuild.Core.Domain.Entities;

namespace QuantumBuild.Core.Infrastructure.Data.Configurations;

public class SiteConfiguration : IEntityTypeConfiguration<Site>
{
    public void Configure(EntityTypeBuilder<Site> builder)
    {
        builder.HasKey(e => e.Id);

        builder.Property(e => e.SiteCode)
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(e => e.SiteName)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(e => e.Address)
            .HasMaxLength(500);

        builder.Property(e => e.City)
            .HasMaxLength(100);

        builder.Property(e => e.PostalCode)
            .HasMaxLength(20);

        builder.Property(e => e.Phone)
            .HasMaxLength(50);

        builder.Property(e => e.Email)
            .HasMaxLength(255);

        builder.Property(e => e.Notes)
            .HasMaxLength(2000);

        // Geolocation fields for attendance geofencing
        builder.Property(e => e.Latitude)
            .HasPrecision(10, 8);

        builder.Property(e => e.Longitude)
            .HasPrecision(11, 8);

        // Float integration fields
        builder.Property(s => s.FloatProjectId);
        builder.Property(s => s.FloatLinkedAt);
        builder.Property(s => s.FloatLinkMethod).HasMaxLength(50);

        builder.HasIndex(e => new { e.TenantId, e.SiteCode })
            .IsUnique()
            .HasDatabaseName("IX_Sites_TenantId_SiteCode");

        // Index on FloatProjectId for quick lookups (where not null)
        builder.HasIndex(s => s.FloatProjectId)
            .HasFilter("\"FloatProjectId\" IS NOT NULL")
            .HasDatabaseName("IX_Sites_FloatProjectId");

        builder.HasOne(e => e.SiteManager)
            .WithMany()
            .HasForeignKey(e => e.SiteManagerId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne(e => e.Company)
            .WithMany(c => c.Sites)
            .HasForeignKey(e => e.CompanyId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
