using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using QuantumBuild.Modules.ToolboxTalks.Domain.Entities;
using QuantumBuild.Modules.ToolboxTalks.Domain.Enums;

namespace QuantumBuild.Modules.ToolboxTalks.Infrastructure.Persistence.Configurations;

/// <summary>
/// Entity Framework configuration for ToolboxTalkSchedule entity
/// </summary>
public class ToolboxTalkScheduleConfiguration : IEntityTypeConfiguration<ToolboxTalkSchedule>
{
    public void Configure(EntityTypeBuilder<ToolboxTalkSchedule> builder)
    {
        // Table name
        builder.ToTable("ToolboxTalkSchedules", "toolbox_talks");

        // Primary key
        builder.HasKey(s => s.Id);

        // Properties
        builder.Property(s => s.ToolboxTalkId)
            .IsRequired();

        builder.Property(s => s.ScheduledDate)
            .IsRequired();

        builder.Property(s => s.EndDate);

        builder.Property(s => s.Frequency)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(50)
            .HasDefaultValue(ToolboxTalkFrequency.Once);

        builder.Property(s => s.AssignToAllEmployees)
            .IsRequired()
            .HasDefaultValue(false);

        builder.Property(s => s.Status)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(50)
            .HasDefaultValue(ToolboxTalkScheduleStatus.Draft);

        builder.Property(s => s.NextRunDate);

        builder.Property(s => s.Notes)
            .HasMaxLength(1000);

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
            .WithMany()
            .HasForeignKey(s => s.ToolboxTalkId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(s => s.Assignments)
            .WithOne(a => a.Schedule)
            .HasForeignKey(a => a.ScheduleId)
            .OnDelete(DeleteBehavior.Cascade);

        // Indexes
        builder.HasIndex(s => new { s.TenantId, s.Status, s.ScheduledDate })
            .HasDatabaseName("ix_toolbox_talk_schedules_tenant_status_date");

        builder.HasIndex(s => s.TenantId)
            .HasDatabaseName("ix_toolbox_talk_schedules_tenant");

        builder.HasIndex(s => s.ToolboxTalkId)
            .HasDatabaseName("ix_toolbox_talk_schedules_talk");

        builder.HasIndex(s => s.Status)
            .HasDatabaseName("ix_toolbox_talk_schedules_status");

        builder.HasIndex(s => s.NextRunDate)
            .HasDatabaseName("ix_toolbox_talk_schedules_next_run");

        // Query filter for soft delete
        builder.HasQueryFilter(s => !s.IsDeleted);
    }
}
