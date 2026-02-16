using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using QuantumBuild.Modules.ToolboxTalks.Domain.Entities;
using QuantumBuild.Modules.ToolboxTalks.Domain.Enums;

namespace QuantumBuild.Modules.ToolboxTalks.Infrastructure.Persistence.Configurations;

/// <summary>
/// Entity Framework configuration for ToolboxTalkQuestion entity
/// </summary>
public class ToolboxTalkQuestionConfiguration : IEntityTypeConfiguration<ToolboxTalkQuestion>
{
    public void Configure(EntityTypeBuilder<ToolboxTalkQuestion> builder)
    {
        // Table name
        builder.ToTable("ToolboxTalkQuestions", "toolbox_talks");

        // Primary key
        builder.HasKey(q => q.Id);

        // Properties
        builder.Property(q => q.ToolboxTalkId)
            .IsRequired();

        builder.Property(q => q.QuestionNumber)
            .IsRequired();

        builder.Property(q => q.QuestionText)
            .IsRequired()
            .HasMaxLength(1000);

        builder.Property(q => q.QuestionType)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(50)
            .HasDefaultValue(QuestionType.MultipleChoice);

        builder.Property(q => q.Options)
            .HasMaxLength(2000);

        builder.Property(q => q.CorrectAnswer)
            .IsRequired()
            .HasMaxLength(500);

        builder.Property(q => q.CorrectOptionIndex);

        builder.Property(q => q.Points)
            .IsRequired()
            .HasDefaultValue(1);

        builder.Property(q => q.Source)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(50)
            .HasDefaultValue(ContentSource.Manual);

        builder.Property(q => q.IsFromVideoFinalPortion)
            .IsRequired()
            .HasDefaultValue(false);

        builder.Property(q => q.VideoTimestamp)
            .HasMaxLength(50);

        // Audit fields
        builder.Property(q => q.CreatedAt)
            .IsRequired();

        builder.Property(q => q.CreatedBy)
            .IsRequired()
            .HasMaxLength(256);

        builder.Property(q => q.UpdatedAt);

        builder.Property(q => q.UpdatedBy)
            .HasMaxLength(256);

        builder.Property(q => q.IsDeleted)
            .IsRequired()
            .HasDefaultValue(false);

        // Relationships - FK to ToolboxTalk with cascade delete
        builder.HasOne(q => q.ToolboxTalk)
            .WithMany(t => t.Questions)
            .HasForeignKey(q => q.ToolboxTalkId)
            .OnDelete(DeleteBehavior.Cascade);

        // Indexes
        builder.HasIndex(q => new { q.ToolboxTalkId, q.QuestionNumber })
            .HasDatabaseName("ix_toolbox_talk_questions_talk_number");

        builder.HasIndex(q => q.ToolboxTalkId)
            .HasDatabaseName("ix_toolbox_talk_questions_talk");

        // Query filter for soft delete
        builder.HasQueryFilter(q => !q.IsDeleted);
    }
}
