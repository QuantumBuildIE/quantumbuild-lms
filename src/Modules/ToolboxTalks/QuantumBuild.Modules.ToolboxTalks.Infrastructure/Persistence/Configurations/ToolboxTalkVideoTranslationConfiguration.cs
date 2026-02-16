using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using QuantumBuild.Modules.ToolboxTalks.Domain.Entities;
using QuantumBuild.Modules.ToolboxTalks.Domain.Enums;

namespace QuantumBuild.Modules.ToolboxTalks.Infrastructure.Persistence.Configurations;

/// <summary>
/// Entity Framework configuration for ToolboxTalkVideoTranslation entity
/// </summary>
public class ToolboxTalkVideoTranslationConfiguration : IEntityTypeConfiguration<ToolboxTalkVideoTranslation>
{
    public void Configure(EntityTypeBuilder<ToolboxTalkVideoTranslation> builder)
    {
        // Table name
        builder.ToTable("ToolboxTalkVideoTranslations", "toolbox_talks");

        // Primary key
        builder.HasKey(v => v.Id);

        // Properties
        builder.Property(v => v.ToolboxTalkId)
            .IsRequired();

        builder.Property(v => v.LanguageCode)
            .IsRequired()
            .HasMaxLength(10);

        builder.Property(v => v.OriginalVideoUrl)
            .IsRequired()
            .HasMaxLength(500);

        builder.Property(v => v.TranslatedVideoUrl)
            .HasMaxLength(500);

        builder.Property(v => v.Status)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(50)
            .HasDefaultValue(VideoTranslationStatus.Pending);

        builder.Property(v => v.ExternalProjectId)
            .HasMaxLength(200);

        builder.Property(v => v.ErrorMessage)
            .HasMaxLength(1000);

        builder.Property(v => v.CompletedAt);

        builder.Property(v => v.TenantId)
            .IsRequired();

        // Audit fields
        builder.Property(v => v.CreatedAt)
            .IsRequired();

        builder.Property(v => v.CreatedBy)
            .IsRequired()
            .HasMaxLength(256);

        builder.Property(v => v.UpdatedAt);

        builder.Property(v => v.UpdatedBy)
            .HasMaxLength(256);

        builder.Property(v => v.IsDeleted)
            .IsRequired()
            .HasDefaultValue(false);

        // Relationships
        builder.HasOne(v => v.ToolboxTalk)
            .WithMany(t => t.VideoTranslations)
            .HasForeignKey(v => v.ToolboxTalkId)
            .OnDelete(DeleteBehavior.Cascade);

        // Indexes
        builder.HasIndex(v => new { v.ToolboxTalkId, v.LanguageCode })
            .IsUnique()
            .HasDatabaseName("ix_toolbox_talk_video_translations_talk_language");

        builder.HasIndex(v => v.ToolboxTalkId)
            .HasDatabaseName("ix_toolbox_talk_video_translations_talk");

        builder.HasIndex(v => v.TenantId)
            .HasDatabaseName("ix_toolbox_talk_video_translations_tenant");

        builder.HasIndex(v => v.Status)
            .HasDatabaseName("ix_toolbox_talk_video_translations_status");

        // Query filter for soft delete
        builder.HasQueryFilter(v => !v.IsDeleted);
    }
}
