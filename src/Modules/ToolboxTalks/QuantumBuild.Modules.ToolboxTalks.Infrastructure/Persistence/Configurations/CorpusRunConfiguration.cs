using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using QuantumBuild.Modules.ToolboxTalks.Domain.Entities;
using QuantumBuild.Modules.ToolboxTalks.Domain.Enums;

namespace QuantumBuild.Modules.ToolboxTalks.Infrastructure.Persistence.Configurations;

public class CorpusRunConfiguration : IEntityTypeConfiguration<CorpusRun>
{
    public void Configure(EntityTypeBuilder<CorpusRun> builder)
    {
        builder.ToTable("CorpusRuns", "toolbox_talks");
        builder.HasKey(r => r.Id);

        builder.Property(r => r.TriggeredBy).HasMaxLength(256);
        builder.Property(r => r.ErrorMessage).HasColumnType("text");
        builder.Property(r => r.IsSmokeTest).HasDefaultValue(false);
        builder.Property(r => r.FailureThresholdPercent).HasDefaultValue(20);
        builder.Property(r => r.ScoreDropThreshold).HasDefaultValue(10);
        builder.Property(r => r.MeanScore).HasColumnType("decimal(6,2)");
        builder.Property(r => r.EstimatedCostEur).HasColumnType("decimal(10,4)");
        builder.Property(r => r.ActualCostEur).HasColumnType("decimal(10,4)");

        builder.Property(r => r.Status)
            .HasConversion<string>()
            .IsRequired();

        builder.Property(r => r.TriggerType)
            .HasConversion<string>()
            .IsRequired();

        builder.Property(r => r.Verdict)
            .HasConversion<string>();

        builder.Property(r => r.CreatedAt).IsRequired();
        builder.Property(r => r.CreatedBy).IsRequired().HasMaxLength(256);
        builder.Property(r => r.UpdatedBy).HasMaxLength(256);
        builder.Property(r => r.IsDeleted).HasDefaultValue(false);

        builder.HasOne(r => r.AuditCorpus)
            .WithMany(c => c.Runs)
            .HasForeignKey(r => r.CorpusId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(r => r.PipelineVersion)
            .WithMany()
            .HasForeignKey(r => r.PipelineVersionId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne(r => r.LinkedPipelineChange)
            .WithMany()
            .HasForeignKey(r => r.LinkedPipelineChangeId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasMany(r => r.Results)
            .WithOne(res => res.CorpusRun)
            .HasForeignKey(res => res.CorpusRunId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(r => new { r.CorpusId, r.Status })
            .HasDatabaseName("ix_corpus_runs_corpus_id_status");

        builder.HasIndex(r => r.TenantId)
            .HasDatabaseName("ix_corpus_runs_tenant_id");

        builder.HasQueryFilter(r => !r.IsDeleted);
    }
}
