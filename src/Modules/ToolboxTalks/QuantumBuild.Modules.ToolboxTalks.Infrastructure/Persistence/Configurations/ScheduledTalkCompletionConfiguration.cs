using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using QuantumBuild.Modules.ToolboxTalks.Domain.Entities;

namespace QuantumBuild.Modules.ToolboxTalks.Infrastructure.Persistence.Configurations;

/// <summary>
/// Entity Framework configuration for ScheduledTalkCompletion entity
/// </summary>
public class ScheduledTalkCompletionConfiguration : IEntityTypeConfiguration<ScheduledTalkCompletion>
{
    public void Configure(EntityTypeBuilder<ScheduledTalkCompletion> builder)
    {
        // Table name
        builder.ToTable("ScheduledTalkCompletions", "toolbox_talks");

        // Primary key
        builder.HasKey(c => c.Id);

        // Properties
        builder.Property(c => c.ScheduledTalkId)
            .IsRequired();

        builder.Property(c => c.CompletedAt)
            .IsRequired();

        builder.Property(c => c.TotalTimeSpentSeconds)
            .IsRequired();

        builder.Property(c => c.VideoWatchPercent);

        builder.Property(c => c.QuizScore);

        builder.Property(c => c.QuizMaxScore);

        builder.Property(c => c.QuizPassed);

        builder.Property(c => c.SignatureData)
            .IsRequired();

        builder.Property(c => c.SignedAt)
            .IsRequired();

        builder.Property(c => c.SignedByName)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(c => c.IPAddress)
            .HasMaxLength(50);

        builder.Property(c => c.UserAgent)
            .HasMaxLength(500);

        builder.Property(c => c.CertificateUrl)
            .HasMaxLength(500);

        // Geolocation when completed
        builder.Property(c => c.CompletedLatitude).HasPrecision(9, 6);
        builder.Property(c => c.CompletedLongitude).HasPrecision(9, 6);
        builder.Property(c => c.CompletedAccuracyMeters).HasPrecision(10, 2);

        // Audit fields
        builder.Property(c => c.CreatedAt)
            .IsRequired();

        builder.Property(c => c.CreatedBy)
            .IsRequired()
            .HasMaxLength(256);

        builder.Property(c => c.UpdatedAt);

        builder.Property(c => c.UpdatedBy)
            .HasMaxLength(256);

        builder.Property(c => c.IsDeleted)
            .IsRequired()
            .HasDefaultValue(false);

        // Relationships - One-to-one with ScheduledTalk
        builder.HasOne(c => c.ScheduledTalk)
            .WithOne(s => s.Completion)
            .HasForeignKey<ScheduledTalkCompletion>(c => c.ScheduledTalkId)
            .OnDelete(DeleteBehavior.Cascade);

        // Indexes - Unique on ScheduledTalkId (one completion per scheduled talk)
        builder.HasIndex(c => c.ScheduledTalkId)
            .IsUnique()
            .HasDatabaseName("ix_scheduled_talk_completions_talk");

        builder.HasIndex(c => c.CompletedAt)
            .HasDatabaseName("ix_scheduled_talk_completions_completed_at");

        // Query filter for soft delete
        builder.HasQueryFilter(c => !c.IsDeleted);
    }
}
