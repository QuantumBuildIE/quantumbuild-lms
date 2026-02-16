using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using QuantumBuild.Modules.ToolboxTalks.Domain.Entities;

namespace QuantumBuild.Modules.ToolboxTalks.Infrastructure.Persistence.Configurations;

/// <summary>
/// Entity Framework configuration for ToolboxTalkCourse entity
/// </summary>
public class ToolboxTalkCourseConfiguration : IEntityTypeConfiguration<ToolboxTalkCourse>
{
    public void Configure(EntityTypeBuilder<ToolboxTalkCourse> builder)
    {
        // Table name
        builder.ToTable("ToolboxTalkCourses", "toolbox_talks");

        // Primary key
        builder.HasKey(x => x.Id);

        // Properties
        builder.Property(x => x.Title)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(x => x.Description)
            .HasMaxLength(2000);

        builder.Property(x => x.IsActive)
            .IsRequired()
            .HasDefaultValue(true);

        builder.Property(x => x.RequireSequentialCompletion)
            .IsRequired()
            .HasDefaultValue(true);

        builder.Property(x => x.RequiresRefresher)
            .IsRequired()
            .HasDefaultValue(false);

        builder.Property(x => x.RefresherIntervalMonths)
            .IsRequired()
            .HasDefaultValue(12);

        builder.Property(x => x.GenerateCertificate)
            .IsRequired()
            .HasDefaultValue(false);

        builder.Property(x => x.AutoAssignToNewEmployees)
            .IsRequired()
            .HasDefaultValue(false);

        builder.Property(x => x.AutoAssignDueDays)
            .IsRequired()
            .HasDefaultValue(14);

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
        builder.HasMany(x => x.CourseItems)
            .WithOne(x => x.Course)
            .HasForeignKey(x => x.CourseId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(x => x.Translations)
            .WithOne(x => x.Course)
            .HasForeignKey(x => x.CourseId)
            .OnDelete(DeleteBehavior.Cascade);

        // Indexes
        builder.HasIndex(x => x.TenantId)
            .HasDatabaseName("ix_toolbox_talk_courses_tenant");

        builder.HasIndex(x => new { x.TenantId, x.IsActive })
            .HasDatabaseName("ix_toolbox_talk_courses_tenant_active");

        // Query filter for soft delete
        builder.HasQueryFilter(x => !x.IsDeleted);
    }
}
