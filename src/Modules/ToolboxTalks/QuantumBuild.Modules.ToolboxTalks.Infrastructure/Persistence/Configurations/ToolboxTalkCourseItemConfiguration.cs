using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using QuantumBuild.Modules.ToolboxTalks.Domain.Entities;

namespace QuantumBuild.Modules.ToolboxTalks.Infrastructure.Persistence.Configurations;

/// <summary>
/// Entity Framework configuration for ToolboxTalkCourseItem entity
/// </summary>
public class ToolboxTalkCourseItemConfiguration : IEntityTypeConfiguration<ToolboxTalkCourseItem>
{
    public void Configure(EntityTypeBuilder<ToolboxTalkCourseItem> builder)
    {
        // Table name
        builder.ToTable("ToolboxTalkCourseItems", "toolbox_talks");

        // Primary key
        builder.HasKey(x => x.Id);

        // Properties
        builder.Property(x => x.CourseId)
            .IsRequired();

        builder.Property(x => x.ToolboxTalkId)
            .IsRequired();

        builder.Property(x => x.OrderIndex)
            .IsRequired();

        builder.Property(x => x.IsRequired)
            .IsRequired()
            .HasDefaultValue(true);

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

        // Relationships
        builder.HasOne(x => x.ToolboxTalk)
            .WithMany()
            .HasForeignKey(x => x.ToolboxTalkId)
            .OnDelete(DeleteBehavior.Restrict);

        // Indexes
        builder.HasIndex(x => x.CourseId)
            .HasDatabaseName("ix_toolbox_talk_course_items_course");

        builder.HasIndex(x => new { x.CourseId, x.OrderIndex })
            .HasDatabaseName("ix_toolbox_talk_course_items_course_order");

        // Unique constraint: same talk can't be in same course twice
        builder.HasIndex(x => new { x.CourseId, x.ToolboxTalkId })
            .IsUnique()
            .HasDatabaseName("ix_toolbox_talk_course_items_course_talk");

        // Query filter for soft delete
        builder.HasQueryFilter(x => !x.IsDeleted);
    }
}
