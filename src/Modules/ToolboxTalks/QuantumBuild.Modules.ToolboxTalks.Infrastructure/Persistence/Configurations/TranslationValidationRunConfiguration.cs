using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using QuantumBuild.Modules.ToolboxTalks.Domain.Entities;
using QuantumBuild.Modules.ToolboxTalks.Domain.Enums;

namespace QuantumBuild.Modules.ToolboxTalks.Infrastructure.Persistence.Configurations;

public class TranslationValidationRunConfiguration : IEntityTypeConfiguration<TranslationValidationRun>
{
    public void Configure(EntityTypeBuilder<TranslationValidationRun> builder)
    {
        builder.ToTable("TranslationValidationRuns", "toolbox_talks");
        builder.HasKey(r => r.Id);

        // Foreign keys (optional — one or the other)
        builder.Property(r => r.ToolboxTalkId);
        builder.Property(r => r.CourseId);

        // Validation configuration
        builder.Property(r => r.LanguageCode)
            .IsRequired()
            .HasMaxLength(10);

        builder.Property(r => r.SectorKey)
            .HasMaxLength(50);

        builder.Property(r => r.PassThreshold)
            .IsRequired();

        builder.Property(r => r.SourceLanguage)
            .IsRequired()
            .HasMaxLength(10);

        builder.Property(r => r.SourceDialect)
            .HasMaxLength(100);

        // Aggregate results
        builder.Property(r => r.OverallScore)
            .IsRequired()
            .HasDefaultValue(0);

        builder.Property(r => r.OverallOutcome)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(50);

        builder.Property(r => r.SafetyVerdict)
            .HasConversion<string>()
            .HasMaxLength(50);

        builder.Property(r => r.TotalSections)
            .IsRequired()
            .HasDefaultValue(0);

        builder.Property(r => r.PassedSections)
            .IsRequired()
            .HasDefaultValue(0);

        builder.Property(r => r.ReviewSections)
            .IsRequired()
            .HasDefaultValue(0);

        builder.Property(r => r.FailedSections)
            .IsRequired()
            .HasDefaultValue(0);

        // Audit metadata
        builder.Property(r => r.ReviewerName)
            .HasMaxLength(200);

        builder.Property(r => r.ReviewerOrg)
            .HasMaxLength(200);

        builder.Property(r => r.ReviewerRole)
            .HasMaxLength(200);

        builder.Property(r => r.DocumentRef)
            .HasMaxLength(100);

        builder.Property(r => r.ClientName)
            .HasMaxLength(200);

        builder.Property(r => r.AuditPurpose)
            .HasMaxLength(500);

        // Pre-flight scan
        builder.Property(r => r.PreFlightScanJson)
            .HasColumnType("text");

        // Pipeline version stamp (nullable — pre-feature runs have null)
        builder.Property(r => r.PipelineVersionId);

        // Run state
        builder.Property(r => r.Status)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(50)
            .HasDefaultValue(ValidationRunStatus.Pending);

        builder.Property(r => r.StartedAt);
        builder.Property(r => r.CompletedAt);

        builder.Property(r => r.AuditReportUrl)
            .HasMaxLength(500);

        builder.Property(r => r.TenantId)
            .IsRequired();

        // Audit fields
        builder.Property(r => r.CreatedAt)
            .IsRequired();

        builder.Property(r => r.CreatedBy)
            .IsRequired()
            .HasMaxLength(256);

        builder.Property(r => r.UpdatedAt);

        builder.Property(r => r.UpdatedBy)
            .HasMaxLength(256);

        builder.Property(r => r.IsDeleted)
            .IsRequired()
            .HasDefaultValue(false);

        // Relationships
        builder.HasOne(r => r.ToolboxTalk)
            .WithMany()
            .HasForeignKey(r => r.ToolboxTalkId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(r => r.Course)
            .WithMany()
            .HasForeignKey(r => r.CourseId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(r => r.Results)
            .WithOne(res => res.ValidationRun)
            .HasForeignKey(res => res.ValidationRunId)
            .OnDelete(DeleteBehavior.Cascade);

        // PipelineVersion FK — relationship managed from PipelineVersionConfiguration
        builder.HasIndex(r => r.PipelineVersionId)
            .HasDatabaseName("ix_translation_validation_runs_pipeline_version");

        // Indexes
        builder.HasIndex(r => r.TenantId)
            .HasDatabaseName("ix_translation_validation_runs_tenant");

        builder.HasIndex(r => r.ToolboxTalkId)
            .HasDatabaseName("ix_translation_validation_runs_talk");

        builder.HasIndex(r => r.CourseId)
            .HasDatabaseName("ix_translation_validation_runs_course");

        builder.HasIndex(r => r.Status)
            .HasDatabaseName("ix_translation_validation_runs_status");

        builder.HasIndex(r => new { r.TenantId, r.ToolboxTalkId, r.LanguageCode })
            .HasDatabaseName("ix_translation_validation_runs_tenant_talk_lang");

        builder.HasIndex(r => new { r.TenantId, r.CourseId, r.LanguageCode })
            .HasDatabaseName("ix_translation_validation_runs_tenant_course_lang");

        // Query filter for soft delete
        builder.HasQueryFilter(r => !r.IsDeleted);
    }
}
