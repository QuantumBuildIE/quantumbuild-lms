using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using QuantumBuild.Modules.ToolboxTalks.Domain.Entities;
using QuantumBuild.Modules.ToolboxTalks.Domain.Enums;

namespace QuantumBuild.Modules.ToolboxTalks.Infrastructure.Persistence.Configurations;

/// <summary>
/// Entity Framework configuration for SubtitleTranslation entity
/// </summary>
public class SubtitleTranslationConfiguration : IEntityTypeConfiguration<SubtitleTranslation>
{
    public void Configure(EntityTypeBuilder<SubtitleTranslation> builder)
    {
        // Table name
        builder.ToTable("SubtitleTranslations", "toolbox_talks");

        // Primary key
        builder.HasKey(t => t.Id);

        // Properties
        builder.Property(t => t.SubtitleProcessingJobId)
            .IsRequired();

        builder.Property(t => t.Language)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(t => t.LanguageCode)
            .IsRequired()
            .HasMaxLength(10);

        builder.Property(t => t.Status)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(50)
            .HasDefaultValue(SubtitleTranslationStatus.Pending);

        builder.Property(t => t.SubtitlesProcessed)
            .HasDefaultValue(0);

        builder.Property(t => t.TotalSubtitles)
            .HasDefaultValue(0);

        // Store SRT content as text (nvarchar(max) / text)
        builder.Property(t => t.SrtContent)
            .HasColumnType("text");

        builder.Property(t => t.SrtUrl)
            .HasMaxLength(500);

        builder.Property(t => t.ErrorMessage)
            .HasMaxLength(2000);

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

        // Relationships
        builder.HasOne(t => t.ProcessingJob)
            .WithMany(j => j.Translations)
            .HasForeignKey(t => t.SubtitleProcessingJobId)
            .OnDelete(DeleteBehavior.Cascade);

        // Indexes
        builder.HasIndex(t => t.SubtitleProcessingJobId)
            .HasDatabaseName("ix_subtitle_translations_job");

        builder.HasIndex(t => new { t.SubtitleProcessingJobId, t.LanguageCode })
            .IsUnique()
            .HasDatabaseName("ix_subtitle_translations_job_language");

        builder.HasIndex(t => t.Status)
            .HasDatabaseName("ix_subtitle_translations_status");

        // Query filter for soft delete
        builder.HasQueryFilter(t => !t.IsDeleted);
    }
}
