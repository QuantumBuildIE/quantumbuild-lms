using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using QuantumBuild.Modules.ToolboxTalks.Domain.Entities;
using QuantumBuild.Modules.ToolboxTalks.Domain.Enums;

namespace QuantumBuild.Modules.ToolboxTalks.Infrastructure.Persistence.Configurations;

public class ValidationRegulatoryScoreConfiguration : IEntityTypeConfiguration<ValidationRegulatoryScore>
{
    public void Configure(EntityTypeBuilder<ValidationRegulatoryScore> builder)
    {
        builder.ToTable("ValidationRegulatoryScores", "toolbox_talks");
        builder.HasKey(s => s.Id);

        // Foreign keys
        builder.Property(s => s.ValidationRunId)
            .IsRequired();

        builder.Property(s => s.RegulatoryProfileId);

        // Score type stored as string
        builder.Property(s => s.ScoreType)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(50);

        builder.Property(s => s.OverallScore)
            .IsRequired()
            .HasDefaultValue(0);

        builder.Property(s => s.CategoryScoresJson)
            .IsRequired()
            .HasColumnType("text");

        builder.Property(s => s.Verdict)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(s => s.Summary)
            .IsRequired()
            .HasMaxLength(2000);

        builder.Property(s => s.RunLabel)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(s => s.RunNumber)
            .IsRequired();

        builder.Property(s => s.FullResponseJson)
            .IsRequired()
            .HasColumnType("text");

        builder.Property(s => s.ScoredSectionCount)
            .IsRequired()
            .HasDefaultValue(0);

        builder.Property(s => s.TargetLanguage)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(s => s.RegulatoryBody)
            .HasMaxLength(20);

        // Tenant
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
        builder.HasOne(s => s.ValidationRun)
            .WithMany()
            .HasForeignKey(s => s.ValidationRunId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(s => s.RegulatoryProfile)
            .WithMany()
            .HasForeignKey(s => s.RegulatoryProfileId)
            .OnDelete(DeleteBehavior.Restrict);

        // Indexes
        builder.HasIndex(s => s.ValidationRunId)
            .HasDatabaseName("ix_validation_regulatory_scores_run");

        builder.HasIndex(s => new { s.ValidationRunId, s.ScoreType })
            .HasDatabaseName("ix_validation_regulatory_scores_run_type");

        builder.HasIndex(s => s.TenantId)
            .HasDatabaseName("ix_validation_regulatory_scores_tenant");

        builder.HasIndex(s => s.RegulatoryProfileId)
            .HasDatabaseName("ix_validation_regulatory_scores_regulatory_profile");

        // Query filter for soft delete
        builder.HasQueryFilter(s => !s.IsDeleted);
    }
}
