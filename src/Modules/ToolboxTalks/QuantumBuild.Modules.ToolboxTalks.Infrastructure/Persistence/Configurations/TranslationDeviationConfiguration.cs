using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using QuantumBuild.Modules.ToolboxTalks.Domain.Entities;
using QuantumBuild.Modules.ToolboxTalks.Domain.Enums;

namespace QuantumBuild.Modules.ToolboxTalks.Infrastructure.Persistence.Configurations;

public class TranslationDeviationConfiguration : IEntityTypeConfiguration<TranslationDeviation>
{
    public void Configure(EntityTypeBuilder<TranslationDeviation> builder)
    {
        builder.ToTable("TranslationDeviations", "toolbox_talks");
        builder.HasKey(d => d.Id);

        builder.Property(d => d.TenantId)
            .IsRequired();

        builder.Property(d => d.DeviationId)
            .IsRequired()
            .HasMaxLength(20);

        builder.Property(d => d.DetectedAt)
            .IsRequired();

        builder.Property(d => d.DetectedBy)
            .IsRequired()
            .HasMaxLength(256);

        builder.Property(d => d.ModuleRef)
            .HasMaxLength(500);

        builder.Property(d => d.LessonRef)
            .HasMaxLength(500);

        builder.Property(d => d.LanguagePair)
            .HasMaxLength(20);

        builder.Property(d => d.SourceExcerpt)
            .HasColumnType("text");

        builder.Property(d => d.TargetExcerpt)
            .HasColumnType("text");

        builder.Property(d => d.Nature)
            .IsRequired()
            .HasColumnType("text");

        builder.Property(d => d.RootCauseCategory)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(d => d.RootCauseDetail)
            .HasColumnType("text");

        builder.Property(d => d.CorrectiveAction)
            .HasColumnType("text");

        builder.Property(d => d.PreventiveAction)
            .HasColumnType("text");

        builder.Property(d => d.Approver)
            .HasMaxLength(256);

        builder.Property(d => d.Status)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(20);

        builder.Property(d => d.ClosedBy)
            .HasMaxLength(256);

        builder.Property(d => d.PipelineVersionAtTime)
            .HasMaxLength(50);

        // Audit fields
        builder.Property(d => d.CreatedAt).IsRequired();
        builder.Property(d => d.CreatedBy).IsRequired().HasMaxLength(256);
        builder.Property(d => d.UpdatedAt);
        builder.Property(d => d.UpdatedBy).HasMaxLength(256);
        builder.Property(d => d.IsDeleted).IsRequired().HasDefaultValue(false);

        // Relationships — SetNull so deviations survive if the run is deleted
        builder.HasOne(d => d.ValidationRun)
            .WithMany()
            .HasForeignKey(d => d.ValidationRunId)
            .OnDelete(DeleteBehavior.SetNull);

        // ValidationResultId has no navigation — plain FK that goes null on delete
        // (no fluent config needed; EF will leave it nullable)

        // DeviationId unique per tenant
        builder.HasIndex(d => new { d.TenantId, d.DeviationId })
            .IsUnique()
            .HasDatabaseName("ix_translation_deviations_tenant_deviation_id");

        // Fast lookup by status + date
        builder.HasIndex(d => new { d.TenantId, d.Status, d.DetectedAt })
            .HasDatabaseName("ix_translation_deviations_tenant_status_detected");

        builder.HasIndex(d => new { d.TenantId, d.ValidationRunId })
            .HasDatabaseName("ix_translation_deviations_tenant_run");

        // Tenant-scoped soft-delete query filter
        builder.HasQueryFilter(d => !d.IsDeleted);
    }
}
