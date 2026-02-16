using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using QuantumBuild.Modules.ToolboxTalks.Domain.Entities;

namespace QuantumBuild.Modules.ToolboxTalks.Infrastructure.Persistence.Configurations;

/// <summary>
/// Entity Framework configuration for ToolboxTalkTranslation entity
/// </summary>
public class ToolboxTalkTranslationConfiguration : IEntityTypeConfiguration<ToolboxTalkTranslation>
{
    public void Configure(EntityTypeBuilder<ToolboxTalkTranslation> builder)
    {
        // Table name
        builder.ToTable("ToolboxTalkTranslations", "toolbox_talks");

        // Primary key
        builder.HasKey(t => t.Id);

        // Properties
        builder.Property(t => t.ToolboxTalkId)
            .IsRequired();

        builder.Property(t => t.LanguageCode)
            .IsRequired()
            .HasMaxLength(10);

        builder.Property(t => t.TranslatedTitle)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(t => t.TranslatedDescription)
            .HasMaxLength(2000);

        builder.Property(t => t.TranslatedSections)
            .IsRequired()
            .HasDefaultValue("[]");

        builder.Property(t => t.TranslatedQuestions);

        builder.Property(t => t.EmailSubject)
            .IsRequired()
            .HasMaxLength(500);

        builder.Property(t => t.EmailBody)
            .IsRequired();

        builder.Property(t => t.TranslatedAt)
            .IsRequired();

        builder.Property(t => t.TranslationProvider)
            .IsRequired()
            .HasMaxLength(50);

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

        // Relationships
        builder.HasOne(t => t.ToolboxTalk)
            .WithMany(tt => tt.Translations)
            .HasForeignKey(t => t.ToolboxTalkId)
            .OnDelete(DeleteBehavior.Cascade);

        // Indexes - Unique on ToolboxTalkId + LanguageCode
        builder.HasIndex(t => new { t.ToolboxTalkId, t.LanguageCode })
            .IsUnique()
            .HasDatabaseName("ix_toolbox_talk_translations_talk_language");

        builder.HasIndex(t => t.ToolboxTalkId)
            .HasDatabaseName("ix_toolbox_talk_translations_talk");

        builder.HasIndex(t => t.TenantId)
            .HasDatabaseName("ix_toolbox_talk_translations_tenant");

        // Query filter for soft delete
        builder.HasQueryFilter(t => !t.IsDeleted);
    }
}
