using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using QuantumBuild.Core.Domain.Entities;

namespace QuantumBuild.Core.Infrastructure.Data.Configurations;

public class EmployeeConfiguration : IEntityTypeConfiguration<Employee>
{
    public void Configure(EntityTypeBuilder<Employee> builder)
    {
        builder.HasKey(e => e.Id);

        builder.Property(e => e.EmployeeCode)
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(e => e.FirstName)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(e => e.LastName)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(e => e.Email)
            .HasMaxLength(255);

        builder.Property(e => e.Phone)
            .HasMaxLength(50);

        builder.Property(e => e.Mobile)
            .HasMaxLength(50);

        builder.Property(e => e.JobTitle)
            .HasMaxLength(100);

        builder.Property(e => e.Department)
            .HasMaxLength(100);

        builder.Property(e => e.UserId);

        builder.Property(e => e.Notes)
            .HasMaxLength(2000);

        builder.Property(e => e.PreferredLanguage)
            .HasMaxLength(10)
            .IsRequired()
            .HasDefaultValue("en");

        builder.Property(e => e.GeoTrackerID)
            .HasMaxLength(10);

        // Float integration fields
        builder.Property(e => e.FloatPersonId);
        builder.Property(e => e.FloatLinkedAt);
        builder.Property(e => e.FloatLinkMethod).HasMaxLength(50);

        // Ignore computed property
        builder.Ignore(e => e.FullName);

        builder.HasIndex(e => new { e.TenantId, e.EmployeeCode })
            .IsUnique()
            .HasDatabaseName("IX_Employees_TenantId_EmployeeCode");

        // Unique index on GeoTrackerID per tenant (where not null)
        builder.HasIndex(e => new { e.TenantId, e.GeoTrackerID })
            .IsUnique()
            .HasFilter("\"GeoTrackerID\" IS NOT NULL")
            .HasDatabaseName("IX_Employees_TenantId_GeoTrackerID");

        // Index on FloatPersonId for quick lookups (where not null)
        builder.HasIndex(e => e.FloatPersonId)
            .HasFilter("\"FloatPersonId\" IS NOT NULL")
            .HasDatabaseName("IX_Employees_FloatPersonId");

        builder.HasOne(e => e.PrimarySite)
            .WithMany()
            .HasForeignKey(e => e.PrimarySiteId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
