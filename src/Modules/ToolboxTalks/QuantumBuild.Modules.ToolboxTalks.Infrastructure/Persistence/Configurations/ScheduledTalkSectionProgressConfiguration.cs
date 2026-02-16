using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using QuantumBuild.Modules.ToolboxTalks.Domain.Entities;

namespace QuantumBuild.Modules.ToolboxTalks.Infrastructure.Persistence.Configurations;

/// <summary>
/// Entity Framework configuration for ScheduledTalkSectionProgress entity
/// </summary>
public class ScheduledTalkSectionProgressConfiguration : IEntityTypeConfiguration<ScheduledTalkSectionProgress>
{
    public void Configure(EntityTypeBuilder<ScheduledTalkSectionProgress> builder)
    {
        // Table name
        builder.ToTable("ScheduledTalkSectionProgress", "toolbox_talks");

        // Primary key
        builder.HasKey(p => p.Id);

        // Properties
        builder.Property(p => p.ScheduledTalkId)
            .IsRequired();

        builder.Property(p => p.SectionId)
            .IsRequired();

        builder.Property(p => p.IsRead)
            .IsRequired()
            .HasDefaultValue(false);

        builder.Property(p => p.ReadAt);

        builder.Property(p => p.TimeSpentSeconds)
            .IsRequired()
            .HasDefaultValue(0);

        // Audit fields
        builder.Property(p => p.CreatedAt)
            .IsRequired();

        builder.Property(p => p.CreatedBy)
            .IsRequired()
            .HasMaxLength(256);

        builder.Property(p => p.UpdatedAt);

        builder.Property(p => p.UpdatedBy)
            .HasMaxLength(256);

        builder.Property(p => p.IsDeleted)
            .IsRequired()
            .HasDefaultValue(false);

        // Relationships
        builder.HasOne(p => p.ScheduledTalk)
            .WithMany(s => s.SectionProgress)
            .HasForeignKey(p => p.ScheduledTalkId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(p => p.Section)
            .WithMany()
            .HasForeignKey(p => p.SectionId)
            .OnDelete(DeleteBehavior.Restrict);

        // Indexes
        builder.HasIndex(p => new { p.ScheduledTalkId, p.SectionId })
            .IsUnique()
            .HasDatabaseName("ix_scheduled_talk_section_progress_talk_section");

        builder.HasIndex(p => p.ScheduledTalkId)
            .HasDatabaseName("ix_scheduled_talk_section_progress_talk");

        builder.HasIndex(p => p.SectionId)
            .HasDatabaseName("ix_scheduled_talk_section_progress_section");

        // Query filter for soft delete
        builder.HasQueryFilter(p => !p.IsDeleted);
    }
}
