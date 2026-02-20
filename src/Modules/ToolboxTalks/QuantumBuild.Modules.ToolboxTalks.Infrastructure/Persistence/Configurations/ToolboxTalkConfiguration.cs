using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using QuantumBuild.Modules.ToolboxTalks.Domain.Entities;
using QuantumBuild.Modules.ToolboxTalks.Domain.Enums;

namespace QuantumBuild.Modules.ToolboxTalks.Infrastructure.Persistence.Configurations;

/// <summary>
/// Entity Framework configuration for ToolboxTalk entity
/// </summary>
public class ToolboxTalkConfiguration : IEntityTypeConfiguration<ToolboxTalk>
{
    public void Configure(EntityTypeBuilder<ToolboxTalk> builder)
    {
        // Table name
        builder.ToTable("ToolboxTalks", "toolbox_talks");

        // Primary key
        builder.HasKey(t => t.Id);

        // Properties
        builder.Property(t => t.Code)
            .IsRequired()
            .HasMaxLength(20);

        builder.Property(t => t.Title)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(t => t.Description)
            .HasMaxLength(2000);

        builder.Property(t => t.Category)
            .HasMaxLength(100);

        builder.Property(t => t.Frequency)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(50)
            .HasDefaultValue(ToolboxTalkFrequency.Once);

        builder.Property(t => t.VideoUrl)
            .HasMaxLength(500);

        builder.Property(t => t.VideoSource)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(50)
            .HasDefaultValue(VideoSource.None);

        builder.Property(t => t.AttachmentUrl)
            .HasMaxLength(500);

        builder.Property(t => t.MinimumVideoWatchPercent)
            .IsRequired()
            .HasDefaultValue(90);

        builder.Property(t => t.RequiresQuiz)
            .IsRequired()
            .HasDefaultValue(false);

        builder.Property(t => t.PassingScore)
            .HasDefaultValue(80);

        builder.Property(t => t.IsActive)
            .IsRequired()
            .HasDefaultValue(true);

        builder.Property(t => t.Status)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(50)
            .HasDefaultValue(ToolboxTalkStatus.Draft);

        builder.Property(t => t.PdfUrl)
            .HasMaxLength(1000);

        builder.Property(t => t.PdfFileName)
            .HasMaxLength(255);

        builder.Property(t => t.GeneratedFromVideo)
            .IsRequired()
            .HasDefaultValue(false);

        builder.Property(t => t.GeneratedFromPdf)
            .IsRequired()
            .HasDefaultValue(false);

        // Extracted PDF text for AI processing - no max length for large documents
        builder.Property(t => t.ExtractedPdfText)
            .HasColumnType("text");

        builder.Property(t => t.PdfTextExtractedAt);

        // Extracted video transcript for AI processing - no max length for long videos
        builder.Property(t => t.ExtractedVideoTranscript)
            .HasColumnType("text");

        builder.Property(t => t.VideoTranscriptExtractedAt);

        // File hash fields for deduplication
        builder.Property(t => t.PdfFileHash)
            .HasMaxLength(64); // SHA-256 produces 64 hex characters

        builder.Property(t => t.VideoFileHash)
            .HasMaxLength(64); // SHA-256 produces 64 hex characters

        builder.Property(t => t.ContentGeneratedAt);

        // Quiz randomization settings
        builder.Property(t => t.QuizQuestionCount);

        builder.Property(t => t.ShuffleQuestions)
            .IsRequired()
            .HasDefaultValue(false);

        builder.Property(t => t.ShuffleOptions)
            .IsRequired()
            .HasDefaultValue(false);

        builder.Property(t => t.UseQuestionPool)
            .IsRequired()
            .HasDefaultValue(false);

        // Refresher settings
        builder.Property(t => t.RequiresRefresher)
            .IsRequired()
            .HasDefaultValue(false);

        builder.Property(t => t.RefresherIntervalMonths)
            .IsRequired()
            .HasDefaultValue(12);

        // Auto-assignment settings
        builder.Property(t => t.AutoAssignToNewEmployees)
            .IsRequired()
            .HasDefaultValue(false);

        builder.Property(t => t.AutoAssignDueDays)
            .IsRequired()
            .HasDefaultValue(14);

        // Source language for translations
        builder.Property(t => t.SourceLanguageCode)
            .HasMaxLength(10)
            .HasDefaultValue("en");

        // PDF slideshow settings
        builder.Property(t => t.GenerateSlidesFromPdf)
            .IsRequired()
            .HasDefaultValue(false);

        builder.Property(t => t.SlidesGenerated)
            .IsRequired()
            .HasDefaultValue(false);

        // AI HTML slideshow
        builder.Property(t => t.SlideshowHtml)
            .HasColumnType("text");

        builder.Property(t => t.SlideshowGeneratedAt);

        builder.Property(t => t.TenantId)
            .IsRequired();

        // Audit fields
        builder.Property(t => t.CreatedAt)
            .IsRequired();

        builder.Property(t => t.CreatedBy)
            .IsRequired()
            .HasMaxLength(256);

        builder.Property(t => t.UpdatedAt);

        builder.Property(t => t.UpdatedBy)
            .HasMaxLength(256);

        builder.Property(t => t.IsDeleted)
            .IsRequired()
            .HasDefaultValue(false);

        // Relationships - Cascade delete for Sections and Questions
        builder.HasMany(t => t.Sections)
            .WithOne(s => s.ToolboxTalk)
            .HasForeignKey(s => s.ToolboxTalkId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(t => t.Questions)
            .WithOne(q => q.ToolboxTalk)
            .HasForeignKey(q => q.ToolboxTalkId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(t => t.Translations)
            .WithOne(tr => tr.ToolboxTalk)
            .HasForeignKey(tr => tr.ToolboxTalkId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(t => t.VideoTranslations)
            .WithOne(vt => vt.ToolboxTalk)
            .HasForeignKey(vt => vt.ToolboxTalkId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(t => t.Slides)
            .WithOne(s => s.ToolboxTalk)
            .HasForeignKey(s => s.ToolboxTalkId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(t => t.SlideshowTranslations)
            .WithOne(st => st.ToolboxTalk)
            .HasForeignKey(st => st.ToolboxTalkId)
            .OnDelete(DeleteBehavior.Cascade);

        // Indexes
        builder.HasIndex(t => new { t.TenantId, t.Code })
            .IsUnique()
            .HasDatabaseName("IX_ToolboxTalks_TenantId_Code");

        builder.HasIndex(t => new { t.TenantId, t.IsDeleted, t.IsActive })
            .HasDatabaseName("ix_toolbox_talks_tenant_deleted_active");

        builder.HasIndex(t => t.TenantId)
            .HasDatabaseName("ix_toolbox_talks_tenant");

        builder.HasIndex(t => t.Title)
            .HasDatabaseName("ix_toolbox_talks_title");

        // Indexes for file hash lookups (deduplication)
        builder.HasIndex(t => new { t.TenantId, t.PdfFileHash })
            .HasDatabaseName("ix_toolbox_talks_tenant_pdf_hash")
            .HasFilter("\"PdfFileHash\" IS NOT NULL");

        builder.HasIndex(t => new { t.TenantId, t.VideoFileHash })
            .HasDatabaseName("ix_toolbox_talks_tenant_video_hash")
            .HasFilter("\"VideoFileHash\" IS NOT NULL");

        // Query filter for soft delete
        builder.HasQueryFilter(t => !t.IsDeleted);
    }
}
