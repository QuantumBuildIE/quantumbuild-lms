using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using QuantumBuild.Modules.ToolboxTalks.Domain.Entities;
using QuantumBuild.Modules.ToolboxTalks.Domain.Enums;

namespace QuantumBuild.Modules.ToolboxTalks.Infrastructure.Persistence.Configurations;

/// <summary>
/// Entity Framework configuration for ScheduledTalk entity
/// </summary>
public class ScheduledTalkConfiguration : IEntityTypeConfiguration<ScheduledTalk>
{
    public void Configure(EntityTypeBuilder<ScheduledTalk> builder)
    {
        // Table name
        builder.ToTable("ScheduledTalks", "toolbox_talks");

        // Primary key
        builder.HasKey(s => s.Id);

        // Properties
        builder.Property(s => s.ToolboxTalkId)
            .IsRequired();

        builder.Property(s => s.EmployeeId)
            .IsRequired();

        builder.Property(s => s.ScheduleId);

        builder.Property(s => s.RequiredDate)
            .IsRequired();

        builder.Property(s => s.DueDate)
            .IsRequired();

        builder.Property(s => s.Status)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(50)
            .HasDefaultValue(ScheduledTalkStatus.Pending);

        builder.Property(s => s.RemindersSent)
            .IsRequired()
            .HasDefaultValue(0);

        builder.Property(s => s.LastReminderAt);

        builder.Property(s => s.LanguageCode)
            .IsRequired()
            .HasMaxLength(10)
            .HasDefaultValue("en");

        builder.Property(s => s.TenantId)
            .IsRequired();

        // Geolocation when started
        builder.Property(s => s.StartedLatitude).HasPrecision(9, 6);
        builder.Property(s => s.StartedLongitude).HasPrecision(9, 6);
        builder.Property(s => s.StartedAccuracyMeters).HasPrecision(10, 2);

        // Refresher tracking
        builder.Property(s => s.IsRefresher)
            .IsRequired()
            .HasDefaultValue(false);

        builder.Property(s => s.OriginalScheduledTalkId);

        builder.Property(s => s.RefresherDueDate);

        builder.Property(s => s.ReminderSent2Weeks)
            .IsRequired()
            .HasDefaultValue(false);

        builder.Property(s => s.ReminderSent1Week)
            .IsRequired()
            .HasDefaultValue(false);

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
            .WithMany()
            .HasForeignKey(s => s.ToolboxTalkId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(s => s.Employee)
            .WithMany()
            .HasForeignKey(s => s.EmployeeId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(s => s.Schedule)
            .WithMany()
            .HasForeignKey(s => s.ScheduleId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne(s => s.CourseAssignment)
            .WithMany(ca => ca.ScheduledTalks)
            .HasForeignKey(s => s.CourseAssignmentId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(s => s.SectionProgress)
            .WithOne(p => p.ScheduledTalk)
            .HasForeignKey(p => p.ScheduledTalkId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(s => s.QuizAttempts)
            .WithOne(q => q.ScheduledTalk)
            .HasForeignKey(q => q.ScheduledTalkId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(s => s.Completion)
            .WithOne(c => c.ScheduledTalk)
            .HasForeignKey<ScheduledTalkCompletion>(c => c.ScheduledTalkId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(s => s.OriginalScheduledTalk)
            .WithMany()
            .HasForeignKey(s => s.OriginalScheduledTalkId)
            .OnDelete(DeleteBehavior.SetNull);

        // Indexes
        builder.HasIndex(s => new { s.TenantId, s.EmployeeId, s.Status })
            .HasDatabaseName("ix_scheduled_talks_tenant_employee_status");

        builder.HasIndex(s => s.TenantId)
            .HasDatabaseName("ix_scheduled_talks_tenant");

        builder.HasIndex(s => s.EmployeeId)
            .HasDatabaseName("ix_scheduled_talks_employee");

        builder.HasIndex(s => s.ToolboxTalkId)
            .HasDatabaseName("ix_scheduled_talks_talk");

        builder.HasIndex(s => s.Status)
            .HasDatabaseName("ix_scheduled_talks_status");

        builder.HasIndex(s => s.DueDate)
            .HasDatabaseName("ix_scheduled_talks_due_date");

        builder.HasIndex(s => s.ScheduleId)
            .HasDatabaseName("ix_scheduled_talks_schedule");

        builder.HasIndex(s => s.CourseAssignmentId)
            .HasDatabaseName("ix_scheduled_talks_course_assignment");

        builder.HasIndex(s => s.OriginalScheduledTalkId)
            .HasDatabaseName("ix_scheduled_talks_original");

        // Query filter for soft delete
        builder.HasQueryFilter(s => !s.IsDeleted);
    }
}
