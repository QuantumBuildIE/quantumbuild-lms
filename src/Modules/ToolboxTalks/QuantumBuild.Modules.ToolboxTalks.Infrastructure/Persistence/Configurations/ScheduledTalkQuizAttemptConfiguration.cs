using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using QuantumBuild.Modules.ToolboxTalks.Domain.Entities;

namespace QuantumBuild.Modules.ToolboxTalks.Infrastructure.Persistence.Configurations;

/// <summary>
/// Entity Framework configuration for ScheduledTalkQuizAttempt entity
/// </summary>
public class ScheduledTalkQuizAttemptConfiguration : IEntityTypeConfiguration<ScheduledTalkQuizAttempt>
{
    public void Configure(EntityTypeBuilder<ScheduledTalkQuizAttempt> builder)
    {
        // Table name
        builder.ToTable("ScheduledTalkQuizAttempts", "toolbox_talks");

        // Primary key
        builder.HasKey(a => a.Id);

        // Properties
        builder.Property(a => a.ScheduledTalkId)
            .IsRequired();

        builder.Property(a => a.AttemptNumber)
            .IsRequired();

        builder.Property(a => a.Answers)
            .IsRequired()
            .HasDefaultValue("{}");

        builder.Property(a => a.Score)
            .IsRequired();

        builder.Property(a => a.MaxScore)
            .IsRequired();

        builder.Property(a => a.Percentage)
            .IsRequired()
            .HasPrecision(5, 2);

        builder.Property(a => a.Passed)
            .IsRequired();

        builder.Property(a => a.AttemptedAt)
            .IsRequired();

        builder.Property(a => a.GeneratedQuestionsJson)
            .HasColumnType("text");

        // Audit fields
        builder.Property(a => a.CreatedAt)
            .IsRequired();

        builder.Property(a => a.CreatedBy)
            .IsRequired()
            .HasMaxLength(256);

        builder.Property(a => a.UpdatedAt);

        builder.Property(a => a.UpdatedBy)
            .HasMaxLength(256);

        builder.Property(a => a.IsDeleted)
            .IsRequired()
            .HasDefaultValue(false);

        // Relationships
        builder.HasOne(a => a.ScheduledTalk)
            .WithMany(s => s.QuizAttempts)
            .HasForeignKey(a => a.ScheduledTalkId)
            .OnDelete(DeleteBehavior.Cascade);

        // Indexes
        builder.HasIndex(a => new { a.ScheduledTalkId, a.AttemptNumber })
            .IsUnique()
            .HasDatabaseName("ix_scheduled_talk_quiz_attempts_talk_attempt");

        builder.HasIndex(a => a.ScheduledTalkId)
            .HasDatabaseName("ix_scheduled_talk_quiz_attempts_talk");

        // Query filter for soft delete
        builder.HasQueryFilter(a => !a.IsDeleted);
    }
}
