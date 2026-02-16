using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using QuantumBuild.Modules.ToolboxTalks.Domain.Entities;

namespace QuantumBuild.Modules.ToolboxTalks.Infrastructure.Persistence.Configurations;

/// <summary>
/// Entity Framework configuration for ToolboxTalkSlide entity
/// </summary>
public class ToolboxTalkSlideConfiguration : IEntityTypeConfiguration<ToolboxTalkSlide>
{
    public void Configure(EntityTypeBuilder<ToolboxTalkSlide> builder)
    {
        // Table name
        builder.ToTable("ToolboxTalkSlides", "toolbox_talks");

        // Primary key
        builder.HasKey(s => s.Id);

        // Properties
        builder.Property(s => s.ImageStoragePath)
            .IsRequired()
            .HasMaxLength(500);

        builder.Property(s => s.OriginalText)
            .HasColumnType("text");

        builder.Property(s => s.TenantId)
            .IsRequired();

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

        // Relationships
        builder.HasOne(s => s.ToolboxTalk)
            .WithMany(t => t.Slides)
            .HasForeignKey(s => s.ToolboxTalkId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(s => s.Translations)
            .WithOne(t => t.Slide)
            .HasForeignKey(t => t.SlideId)
            .OnDelete(DeleteBehavior.Cascade);

        // Indexes
        builder.HasIndex(s => s.ToolboxTalkId)
            .HasDatabaseName("ix_toolbox_talk_slides_talk");

        builder.HasIndex(s => new { s.ToolboxTalkId, s.PageNumber })
            .IsUnique()
            .HasFilter("\"IsDeleted\" = false")
            .HasDatabaseName("ix_toolbox_talk_slides_talk_page");

        // Query filter for soft delete
        builder.HasQueryFilter(s => !s.IsDeleted);
    }
}
