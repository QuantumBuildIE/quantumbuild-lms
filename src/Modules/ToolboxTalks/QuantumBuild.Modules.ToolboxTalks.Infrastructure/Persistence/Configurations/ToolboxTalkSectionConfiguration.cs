using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using QuantumBuild.Modules.ToolboxTalks.Domain.Entities;
using QuantumBuild.Modules.ToolboxTalks.Domain.Enums;

namespace QuantumBuild.Modules.ToolboxTalks.Infrastructure.Persistence.Configurations;

/// <summary>
/// Entity Framework configuration for ToolboxTalkSection entity
/// </summary>
public class ToolboxTalkSectionConfiguration : IEntityTypeConfiguration<ToolboxTalkSection>
{
    public void Configure(EntityTypeBuilder<ToolboxTalkSection> builder)
    {
        // Table name
        builder.ToTable("ToolboxTalkSections", "toolbox_talks");

        // Primary key
        builder.HasKey(s => s.Id);

        // Properties
        builder.Property(s => s.ToolboxTalkId)
            .IsRequired();

        builder.Property(s => s.SectionNumber)
            .IsRequired();

        builder.Property(s => s.Title)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(s => s.Content)
            .IsRequired();

        builder.Property(s => s.RequiresAcknowledgment)
            .IsRequired()
            .HasDefaultValue(true);

        builder.Property(s => s.Source)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(50)
            .HasDefaultValue(ContentSource.Manual);

        builder.Property(s => s.VideoTimestamp)
            .HasMaxLength(50);

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

        // Relationships - FK to ToolboxTalk with cascade delete
        builder.HasOne(s => s.ToolboxTalk)
            .WithMany(t => t.Sections)
            .HasForeignKey(s => s.ToolboxTalkId)
            .OnDelete(DeleteBehavior.Cascade);

        // Indexes
        builder.HasIndex(s => new { s.ToolboxTalkId, s.SectionNumber })
            .HasDatabaseName("ix_toolbox_talk_sections_talk_number");

        builder.HasIndex(s => s.ToolboxTalkId)
            .HasDatabaseName("ix_toolbox_talk_sections_talk");

        // Query filter for soft delete
        builder.HasQueryFilter(s => !s.IsDeleted);
    }
}
