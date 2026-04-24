using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using QuantumBuild.Modules.ToolboxTalks.Domain.Entities;
using QuantumBuild.Modules.ToolboxTalks.Domain.Enums;

namespace QuantumBuild.Modules.ToolboxTalks.Infrastructure.Persistence.Configurations;

public class PipelineChangeRecordConfiguration : IEntityTypeConfiguration<PipelineChangeRecord>
{
    public void Configure(EntityTypeBuilder<PipelineChangeRecord> builder)
    {
        builder.ToTable("PipelineChangeRecords", "toolbox_talks");
        builder.HasKey(cr => cr.Id);

        builder.Property(cr => cr.ChangeId)
            .IsRequired()
            .HasMaxLength(20);

        builder.Property(cr => cr.Component)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(cr => cr.ChangeFrom)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(cr => cr.ChangeTo)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(cr => cr.Justification)
            .IsRequired()
            .HasColumnType("text");

        builder.Property(cr => cr.ImpactAssessment)
            .HasColumnType("text");

        builder.Property(cr => cr.PriorModulesAction)
            .HasColumnType("text");

        builder.Property(cr => cr.Approver)
            .HasMaxLength(200);

        builder.Property(cr => cr.DeployedAt)
            .IsRequired();

        builder.Property(cr => cr.Status)
            .HasConversion<string>()
            .IsRequired()
            .HasDefaultValue(PipelineChangeStatus.Draft);

        builder.Property(cr => cr.PipelineVersionId)
            .IsRequired();

        builder.Property(cr => cr.PreviousPipelineVersionId);

        // Audit fields
        builder.Property(cr => cr.CreatedAt)
            .IsRequired();

        builder.Property(cr => cr.CreatedBy)
            .IsRequired()
            .HasMaxLength(256);

        builder.Property(cr => cr.UpdatedAt);

        builder.Property(cr => cr.UpdatedBy)
            .HasMaxLength(256);

        builder.Property(cr => cr.IsDeleted)
            .IsRequired()
            .HasDefaultValue(false);

        // Relationship to PipelineVersion (configured from the PipelineVersion side)
        // Omit HasOne here — EF resolves the relationship from PipelineVersionConfiguration

        // Indexes
        builder.HasIndex(cr => cr.PipelineVersionId)
            .HasDatabaseName("ix_pipeline_change_records_version");

        builder.HasIndex(cr => cr.ChangeId)
            .IsUnique()
            .HasDatabaseName("ix_pipeline_change_records_change_id");

        // Soft-delete query filter
        builder.HasQueryFilter(cr => !cr.IsDeleted);
    }
}
