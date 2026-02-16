using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using QuantumBuild.Modules.ToolboxTalks.Domain.Entities;
using QuantumBuild.Modules.ToolboxTalks.Domain.Enums;

namespace QuantumBuild.Modules.ToolboxTalks.Infrastructure.Persistence.Configurations;

/// <summary>
/// Entity Framework configuration for ToolboxTalkCertificate entity
/// </summary>
public class ToolboxTalkCertificateConfiguration : IEntityTypeConfiguration<ToolboxTalkCertificate>
{
    public void Configure(EntityTypeBuilder<ToolboxTalkCertificate> builder)
    {
        // Table name
        builder.ToTable("ToolboxTalkCertificates", "toolbox_talks");

        // Primary key
        builder.HasKey(c => c.Id);

        // Properties
        builder.Property(c => c.EmployeeId)
            .IsRequired();

        builder.Property(c => c.CertificateType)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(50);

        builder.Property(c => c.ToolboxTalkId);

        builder.Property(c => c.ScheduledTalkId);

        builder.Property(c => c.CourseId);

        builder.Property(c => c.CourseAssignmentId);

        builder.Property(c => c.CertificateNumber)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(c => c.IssuedAt)
            .IsRequired();

        builder.Property(c => c.ExpiresAt);

        builder.Property(c => c.PdfStoragePath)
            .IsRequired()
            .HasMaxLength(500);

        builder.Property(c => c.IsRefresher)
            .IsRequired()
            .HasDefaultValue(false);

        builder.Property(c => c.EmployeeName)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(c => c.EmployeeCode)
            .HasMaxLength(50);

        builder.Property(c => c.TrainingTitle)
            .IsRequired()
            .HasMaxLength(500);

        builder.Property(c => c.IncludedTalksJson);

        builder.Property(c => c.SignatureDataUrl);

        builder.Property(c => c.TenantId)
            .IsRequired();

        // Audit fields
        builder.Property(c => c.CreatedAt)
            .IsRequired();

        builder.Property(c => c.CreatedBy)
            .IsRequired()
            .HasMaxLength(256);

        builder.Property(c => c.UpdatedAt);

        builder.Property(c => c.UpdatedBy)
            .HasMaxLength(256);

        builder.Property(c => c.IsDeleted)
            .IsRequired()
            .HasDefaultValue(false);

        // Relationships
        builder.HasOne(c => c.Employee)
            .WithMany()
            .HasForeignKey(c => c.EmployeeId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(c => c.ToolboxTalk)
            .WithMany()
            .HasForeignKey(c => c.ToolboxTalkId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne(c => c.Course)
            .WithMany()
            .HasForeignKey(c => c.CourseId)
            .OnDelete(DeleteBehavior.SetNull);

        // Indexes
        builder.HasIndex(c => c.CertificateNumber)
            .IsUnique()
            .HasDatabaseName("ix_toolbox_talk_certificates_number");

        builder.HasIndex(c => c.TenantId)
            .HasDatabaseName("ix_toolbox_talk_certificates_tenant");

        builder.HasIndex(c => c.EmployeeId)
            .HasDatabaseName("ix_toolbox_talk_certificates_employee");

        builder.HasIndex(c => new { c.TenantId, c.EmployeeId })
            .HasDatabaseName("ix_toolbox_talk_certificates_tenant_employee");

        // Query filter for soft delete
        builder.HasQueryFilter(c => !c.IsDeleted);
    }
}
