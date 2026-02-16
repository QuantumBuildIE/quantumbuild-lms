using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using QuantumBuild.Modules.ToolboxTalks.Domain.Entities;
using QuantumBuild.Modules.ToolboxTalks.Domain.Enums;

namespace QuantumBuild.Modules.ToolboxTalks.Infrastructure.Persistence.Configurations;

/// <summary>
/// Entity Framework configuration for ToolboxTalkCourseAssignment entity
/// </summary>
public class ToolboxTalkCourseAssignmentConfiguration : IEntityTypeConfiguration<ToolboxTalkCourseAssignment>
{
    public void Configure(EntityTypeBuilder<ToolboxTalkCourseAssignment> builder)
    {
        // Table name
        builder.ToTable("ToolboxTalkCourseAssignments", "toolbox_talks");

        // Primary key
        builder.HasKey(x => x.Id);

        // Properties
        builder.Property(x => x.CourseId)
            .IsRequired();

        builder.Property(x => x.EmployeeId)
            .IsRequired();

        builder.Property(x => x.AssignedAt)
            .IsRequired();

        builder.Property(x => x.DueDate);

        builder.Property(x => x.StartedAt);

        builder.Property(x => x.CompletedAt);

        builder.Property(x => x.Status)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(20)
            .HasDefaultValue(CourseAssignmentStatus.Assigned);

        builder.Property(x => x.IsRefresher)
            .IsRequired()
            .HasDefaultValue(false);

        builder.Property(x => x.OriginalAssignmentId);

        builder.Property(x => x.RefresherDueDate);

        builder.Property(x => x.ReminderSent2Weeks)
            .IsRequired()
            .HasDefaultValue(false);

        builder.Property(x => x.ReminderSent1Week)
            .IsRequired()
            .HasDefaultValue(false);

        builder.Property(x => x.TenantId)
            .IsRequired();

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
        builder.HasOne(x => x.Course)
            .WithMany()
            .HasForeignKey(x => x.CourseId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.Employee)
            .WithMany()
            .HasForeignKey(x => x.EmployeeId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.OriginalAssignment)
            .WithMany()
            .HasForeignKey(x => x.OriginalAssignmentId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasMany(x => x.ScheduledTalks)
            .WithOne(x => x.CourseAssignment)
            .HasForeignKey(x => x.CourseAssignmentId)
            .OnDelete(DeleteBehavior.Cascade);

        // Indexes
        builder.HasIndex(x => x.TenantId)
            .HasDatabaseName("ix_course_assignments_tenant");

        builder.HasIndex(x => x.CourseId)
            .HasDatabaseName("ix_course_assignments_course");

        builder.HasIndex(x => x.EmployeeId)
            .HasDatabaseName("ix_course_assignments_employee");

        builder.HasIndex(x => new { x.TenantId, x.Status })
            .HasDatabaseName("ix_course_assignments_tenant_status");

        builder.HasIndex(x => new { x.CourseId, x.EmployeeId })
            .HasDatabaseName("ix_course_assignments_course_employee");

        // Query filter for soft delete
        builder.HasQueryFilter(x => !x.IsDeleted);
    }
}
