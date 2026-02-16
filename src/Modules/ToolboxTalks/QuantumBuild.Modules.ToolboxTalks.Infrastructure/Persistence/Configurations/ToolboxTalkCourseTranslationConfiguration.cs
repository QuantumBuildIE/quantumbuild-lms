using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using QuantumBuild.Modules.ToolboxTalks.Domain.Entities;

namespace QuantumBuild.Modules.ToolboxTalks.Infrastructure.Persistence.Configurations;

/// <summary>
/// Entity Framework configuration for ToolboxTalkCourseTranslation entity
/// </summary>
public class ToolboxTalkCourseTranslationConfiguration : IEntityTypeConfiguration<ToolboxTalkCourseTranslation>
{
    public void Configure(EntityTypeBuilder<ToolboxTalkCourseTranslation> builder)
    {
        // Table name
        builder.ToTable("ToolboxTalkCourseTranslations", "toolbox_talks");

        // Primary key
        builder.HasKey(x => x.Id);

        // Properties
        builder.Property(x => x.CourseId)
            .IsRequired();

        builder.Property(x => x.LanguageCode)
            .IsRequired()
            .HasMaxLength(10);

        builder.Property(x => x.TranslatedTitle)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(x => x.TranslatedDescription)
            .HasMaxLength(2000);

        // Audit fields
        builder.Property(x => x.CreatedAt)
            .IsRequired();

        builder.Property(x => x.CreatedBy)
            .IsRequired()
            .HasMaxLength(256);

        builder.Property(x => x.UpdatedAt);

        builder.Property(x => x.UpdatedBy)
            .HasMaxLength(256);

        builder.Property(x => x.IsDeleted)
            .IsRequired()
            .HasDefaultValue(false);

        // Indexes
        // Unique constraint: one translation per language per course
        builder.HasIndex(x => new { x.CourseId, x.LanguageCode })
            .IsUnique()
            .HasDatabaseName("ix_toolbox_talk_course_translations_course_language");

        builder.HasIndex(x => x.CourseId)
            .HasDatabaseName("ix_toolbox_talk_course_translations_course");

        // Query filter for soft delete
        builder.HasQueryFilter(x => !x.IsDeleted);
    }
}
