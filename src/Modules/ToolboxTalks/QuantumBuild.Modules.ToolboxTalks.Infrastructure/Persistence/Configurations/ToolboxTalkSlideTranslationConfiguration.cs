using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using QuantumBuild.Modules.ToolboxTalks.Domain.Entities;

namespace QuantumBuild.Modules.ToolboxTalks.Infrastructure.Persistence.Configurations;

/// <summary>
/// Entity Framework configuration for ToolboxTalkSlideTranslation entity
/// </summary>
public class ToolboxTalkSlideTranslationConfiguration : IEntityTypeConfiguration<ToolboxTalkSlideTranslation>
{
    public void Configure(EntityTypeBuilder<ToolboxTalkSlideTranslation> builder)
    {
        // Table name
        builder.ToTable("ToolboxTalkSlideTranslations", "toolbox_talks");

        // Primary key
        builder.HasKey(t => t.Id);

        // Properties
        builder.Property(t => t.LanguageCode)
            .IsRequired()
            .HasMaxLength(10);

        builder.Property(t => t.TranslatedText)
            .IsRequired()
            .HasColumnType("text");

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
        builder.HasOne(t => t.Slide)
            .WithMany(s => s.Translations)
            .HasForeignKey(t => t.SlideId)
            .OnDelete(DeleteBehavior.Cascade);

        // Indexes - Unique on SlideId + LanguageCode
        builder.HasIndex(t => new { t.SlideId, t.LanguageCode })
            .IsUnique()
            .HasDatabaseName("ix_toolbox_talk_slide_translations_slide_language");
    }
}
