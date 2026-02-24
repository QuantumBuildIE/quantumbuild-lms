using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using QuantumBuild.Modules.LessonParser.Domain.Entities;
using QuantumBuild.Modules.LessonParser.Domain.Enums;

namespace QuantumBuild.Modules.LessonParser.Infrastructure.Persistence.Configurations;

/// <summary>
/// Entity Framework configuration for ParseJob entity
/// </summary>
public class ParseJobConfiguration : IEntityTypeConfiguration<ParseJob>
{
    public void Configure(EntityTypeBuilder<ParseJob> builder)
    {
        // Table name
        builder.ToTable("ParseJobs", "lesson_parser");

        // Primary key
        builder.HasKey(p => p.Id);

        // Properties
        builder.Property(p => p.InputType)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(50);

        builder.Property(p => p.InputReference)
            .IsRequired()
            .HasMaxLength(500);

        builder.Property(p => p.Status)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(50)
            .HasDefaultValue(ParseJobStatus.Processing);

        builder.Property(p => p.GeneratedCourseId);

        builder.Property(p => p.GeneratedCourseTitle)
            .HasMaxLength(200);

        builder.Property(p => p.TalksGenerated)
            .IsRequired()
            .HasDefaultValue(0);

        builder.Property(p => p.ErrorMessage)
            .HasMaxLength(2000);

        // No max length — extracted content can be large (full document text)
        builder.Property(p => p.ExtractedContent);

        builder.Property(p => p.TenantId)
            .IsRequired();

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

        // Indexes
        builder.HasIndex(p => p.TenantId)
            .HasDatabaseName("ix_parse_jobs_tenant");

        builder.HasIndex(p => new { p.TenantId, p.Status })
            .HasDatabaseName("ix_parse_jobs_tenant_status");
    }
}
