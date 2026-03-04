using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using QuantumBuild.Modules.ToolboxTalks.Domain.Entities;
using QuantumBuild.Modules.ToolboxTalks.Domain.Enums;

namespace QuantumBuild.Modules.ToolboxTalks.Infrastructure.Persistence.Configurations;

public class ContentCreationSessionConfiguration : IEntityTypeConfiguration<ContentCreationSession>
{
    public void Configure(EntityTypeBuilder<ContentCreationSession> builder)
    {
        builder.ToTable("ContentCreationSessions", "toolbox_talks");
        builder.HasKey(s => s.Id);

        // Input configuration
        builder.Property(s => s.InputMode)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(20);

        builder.Property(s => s.Status)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(50)
            .HasDefaultValue(ContentCreationSessionStatus.Draft);

        // Source content — Text
        builder.Property(s => s.SourceText)
            .HasColumnType("text");

        // Source content — File
        builder.Property(s => s.SourceFileName)
            .HasMaxLength(500);

        builder.Property(s => s.SourceFileUrl)
            .HasMaxLength(1000);

        builder.Property(s => s.SourceFileType)
            .HasMaxLength(20);

        // Transcript
        builder.Property(s => s.TranscriptText)
            .HasColumnType("text");

        // Parsed content
        builder.Property(s => s.ParsedSectionsJson)
            .HasColumnType("text");

        // Output configuration
        builder.Property(s => s.OutputType)
            .HasConversion<string>()
            .HasMaxLength(20);

        builder.Property(s => s.OutputId);

        // Translation & validation
        builder.Property(s => s.TargetLanguageCodes)
            .HasMaxLength(500);

        builder.Property(s => s.PassThreshold)
            .IsRequired()
            .HasDefaultValue(75);

        builder.Property(s => s.SectorKey)
            .HasMaxLength(50);

        // Audit metadata
        builder.Property(s => s.ReviewerName)
            .HasMaxLength(200);

        builder.Property(s => s.ReviewerOrg)
            .HasMaxLength(200);

        builder.Property(s => s.ReviewerRole)
            .HasMaxLength(200);

        builder.Property(s => s.DocumentRef)
            .HasMaxLength(100);

        builder.Property(s => s.ClientName)
            .HasMaxLength(200);

        builder.Property(s => s.AuditPurpose)
            .HasMaxLength(500);

        // Session lifecycle
        builder.Property(s => s.ExpiresAt)
            .IsRequired();

        // Validation run tracking
        builder.Property(s => s.ValidationRunIds)
            .HasColumnType("text");

        // Tenant
        builder.Property(s => s.TenantId)
            .IsRequired();

        // Audit fields
        builder.Property(s => s.CreatedAt)
            .IsRequired();

        builder.Property(s => s.CreatedBy)
            .IsRequired()
            .HasMaxLength(256);

        builder.Property(s => s.UpdatedAt);

        builder.Property(s => s.UpdatedBy)
            .HasMaxLength(256);

        builder.Property(s => s.IsDeleted)
            .IsRequired()
            .HasDefaultValue(false);

        // Relationships — optional FK to Talk or Course created from this session
        builder.HasOne(s => s.OutputTalk)
            .WithMany()
            .HasForeignKey(s => s.OutputId)
            .HasConstraintName("fk_content_creation_sessions_talk")
            .IsRequired(false)
            .OnDelete(DeleteBehavior.SetNull);

        // Note: OutputId can point to either a Talk or Course.
        // We use a single nullable FK column; the OutputType enum disambiguates.
        // Only one navigation is configured to avoid EF conflicts.
        // OutputCourse is left unmapped as a convenience property.
        builder.Ignore(s => s.OutputCourse);

        // Indexes
        builder.HasIndex(s => s.TenantId)
            .HasDatabaseName("ix_content_creation_sessions_tenant");

        builder.HasIndex(s => s.Status)
            .HasDatabaseName("ix_content_creation_sessions_status");

        builder.HasIndex(s => s.ExpiresAt)
            .HasDatabaseName("ix_content_creation_sessions_expires");

        builder.HasIndex(s => new { s.TenantId, s.Status })
            .HasDatabaseName("ix_content_creation_sessions_tenant_status");

        // Query filter for soft delete
        builder.HasQueryFilter(s => !s.IsDeleted);
    }
}
