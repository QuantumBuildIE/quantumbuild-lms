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

        builder.Property(s => s.OutputTalkId);
        builder.Property(s => s.OutputCourseId);

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

        // Quiz
        builder.Property(s => s.QuestionsJson)
            .HasColumnType("text");

        builder.Property(s => s.QuizSettingsJson)
            .HasColumnType("text");

        // Settings
        builder.Property(s => s.SettingsJson)
            .HasColumnType("text");

        // Subtitle processing
        builder.Property(s => s.SubtitleJobId)
            .HasMaxLength(100);

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

        // Relationships — separate FKs to Talk and Course
        builder.HasOne(s => s.OutputTalk)
            .WithMany()
            .HasForeignKey(s => s.OutputTalkId)
            .HasConstraintName("fk_content_creation_sessions_talk")
            .IsRequired(false)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne(s => s.OutputCourse)
            .WithMany()
            .HasForeignKey(s => s.OutputCourseId)
            .HasConstraintName("fk_content_creation_sessions_course")
            .IsRequired(false)
            .OnDelete(DeleteBehavior.SetNull);

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
