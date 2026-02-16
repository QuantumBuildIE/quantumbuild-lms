using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using QuantumBuild.Modules.ToolboxTalks.Domain.Entities;
using QuantumBuild.Modules.ToolboxTalks.Domain.Enums;

namespace QuantumBuild.Modules.ToolboxTalks.Infrastructure.Persistence.Configurations;

/// <summary>
/// Entity Framework configuration for SubtitleProcessingJob entity
/// </summary>
public class SubtitleProcessingJobConfiguration : IEntityTypeConfiguration<SubtitleProcessingJob>
{
    public void Configure(EntityTypeBuilder<SubtitleProcessingJob> builder)
    {
        // Table name
        builder.ToTable("SubtitleProcessingJobs", "toolbox_talks");

        // Primary key
        builder.HasKey(j => j.Id);

        // Properties
        builder.Property(j => j.ToolboxTalkId)
            .IsRequired();

        builder.Property(j => j.Status)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(50)
            .HasDefaultValue(SubtitleProcessingStatus.Pending);

        builder.Property(j => j.ErrorMessage)
            .HasMaxLength(2000);

        builder.Property(j => j.SourceVideoUrl)
            .IsRequired()
            .HasMaxLength(1000);

        builder.Property(j => j.VideoSourceType)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(50);

        builder.Property(j => j.StartedAt);

        builder.Property(j => j.CompletedAt);

        builder.Property(j => j.TotalSubtitles)
            .HasDefaultValue(0);

        // Store SRT content as text (nvarchar(max) / text)
        builder.Property(j => j.EnglishSrtContent)
            .HasColumnType("text");

        builder.Property(j => j.EnglishSrtUrl)
            .HasMaxLength(500);

        builder.Property(j => j.TenantId)
            .IsRequired();

        // Audit fields
        builder.Property(j => j.CreatedAt)
            .IsRequired();

        builder.Property(j => j.CreatedBy)
            .IsRequired()
            .HasMaxLength(256);

        builder.Property(j => j.UpdatedAt);

        builder.Property(j => j.UpdatedBy)
            .HasMaxLength(256);

        builder.Property(j => j.IsDeleted)
            .IsRequired()
            .HasDefaultValue(false);

        // Relationships
        builder.HasOne(j => j.ToolboxTalk)
            .WithMany()
            .HasForeignKey(j => j.ToolboxTalkId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(j => j.Translations)
            .WithOne(t => t.ProcessingJob)
            .HasForeignKey(t => t.SubtitleProcessingJobId)
            .OnDelete(DeleteBehavior.Cascade);

        // Indexes
        builder.HasIndex(j => j.ToolboxTalkId)
            .HasDatabaseName("ix_subtitle_processing_jobs_toolbox_talk");

        builder.HasIndex(j => j.TenantId)
            .HasDatabaseName("ix_subtitle_processing_jobs_tenant");

        builder.HasIndex(j => j.Status)
            .HasDatabaseName("ix_subtitle_processing_jobs_status");

        builder.HasIndex(j => new { j.TenantId, j.Status })
            .HasDatabaseName("ix_subtitle_processing_jobs_tenant_status");

        builder.HasIndex(j => new { j.TenantId, j.ToolboxTalkId, j.IsDeleted })
            .HasDatabaseName("ix_subtitle_processing_jobs_tenant_talk_deleted");

        // Query filter for soft delete
        builder.HasQueryFilter(j => !j.IsDeleted);
    }
}
