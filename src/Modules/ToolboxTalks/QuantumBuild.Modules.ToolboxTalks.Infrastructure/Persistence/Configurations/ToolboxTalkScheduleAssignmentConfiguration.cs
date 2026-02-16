using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using QuantumBuild.Modules.ToolboxTalks.Domain.Entities;

namespace QuantumBuild.Modules.ToolboxTalks.Infrastructure.Persistence.Configurations;

/// <summary>
/// Entity Framework configuration for ToolboxTalkScheduleAssignment entity
/// </summary>
public class ToolboxTalkScheduleAssignmentConfiguration : IEntityTypeConfiguration<ToolboxTalkScheduleAssignment>
{
    public void Configure(EntityTypeBuilder<ToolboxTalkScheduleAssignment> builder)
    {
        // Table name
        builder.ToTable("ToolboxTalkScheduleAssignments", "toolbox_talks");

        // Primary key
        builder.HasKey(a => a.Id);

        // Properties
        builder.Property(a => a.ScheduleId)
            .IsRequired();

        builder.Property(a => a.EmployeeId)
            .IsRequired();

        builder.Property(a => a.IsProcessed)
            .IsRequired()
            .HasDefaultValue(false);

        builder.Property(a => a.ProcessedAt);

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
        builder.HasOne(a => a.Schedule)
            .WithMany(s => s.Assignments)
            .HasForeignKey(a => a.ScheduleId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(a => a.Employee)
            .WithMany()
            .HasForeignKey(a => a.EmployeeId)
            .OnDelete(DeleteBehavior.Restrict);

        // Indexes
        builder.HasIndex(a => new { a.ScheduleId, a.EmployeeId })
            .IsUnique()
            .HasDatabaseName("ix_toolbox_talk_schedule_assignments_schedule_employee");

        builder.HasIndex(a => a.ScheduleId)
            .HasDatabaseName("ix_toolbox_talk_schedule_assignments_schedule");

        builder.HasIndex(a => a.EmployeeId)
            .HasDatabaseName("ix_toolbox_talk_schedule_assignments_employee");

        // Query filter for soft delete
        builder.HasQueryFilter(a => !a.IsDeleted);
    }
}
