using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using QuantumBuild.Modules.ToolboxTalks.Domain.Entities;

namespace QuantumBuild.Modules.ToolboxTalks.Infrastructure.Persistence.Configurations;

/// <summary>
/// Entity Framework configuration for ToolboxTalkSlideshowTranslation entity
/// </summary>
public class ToolboxTalkSlideshowTranslationConfiguration : IEntityTypeConfiguration<ToolboxTalkSlideshowTranslation>
{
    public void Configure(EntityTypeBuilder<ToolboxTalkSlideshowTranslation> builder)
    {
        // Table name
        builder.ToTable("ToolboxTalkSlideshowTranslations", "toolbox_talks");

        // Primary key
        builder.HasKey(t => t.Id);

        // Properties
        builder.Property(t => t.LanguageCode)
            .IsRequired()
            .HasMaxLength(10);

        builder.Property(t => t.TranslatedHtml)
            .IsRequired();

        builder.Property(t => t.TranslatedAt)
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

        // Relationships
        builder.HasOne(t => t.ToolboxTalk)
            .WithMany(tt => tt.SlideshowTranslations)
            .HasForeignKey(t => t.ToolboxTalkId)
            .OnDelete(DeleteBehavior.Cascade);

        // Indexes - Unique on ToolboxTalkId + LanguageCode
        builder.HasIndex(t => new { t.ToolboxTalkId, t.LanguageCode })
            .IsUnique()
            .HasDatabaseName("ix_toolbox_talk_slideshow_translations_talk_language");
    }
}
