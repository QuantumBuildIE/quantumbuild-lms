using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using QuantumBuild.Modules.ToolboxTalks.Domain.Entities;
using QuantumBuild.Modules.ToolboxTalks.Domain.Enums;

namespace QuantumBuild.Modules.ToolboxTalks.Infrastructure.Persistence.Configurations;

public class TranslationValidationResultConfiguration : IEntityTypeConfiguration<TranslationValidationResult>
{
    public void Configure(EntityTypeBuilder<TranslationValidationResult> builder)
    {
        builder.ToTable("TranslationValidationResults", "toolbox_talks");
        builder.HasKey(r => r.Id);

        builder.Property(r => r.ValidationRunId)
            .IsRequired();

        // Section identification
        builder.Property(r => r.SectionIndex)
            .IsRequired();

        builder.Property(r => r.SectionTitle)
            .IsRequired()
            .HasMaxLength(500);

        // Source and translation text
        builder.Property(r => r.OriginalText)
            .IsRequired()
            .HasColumnType("text");

        builder.Property(r => r.TranslatedText)
            .IsRequired()
            .HasColumnType("text");

        // Back-translations
        builder.Property(r => r.BackTranslationA)
            .HasColumnType("text");

        builder.Property(r => r.BackTranslationB)
            .HasColumnType("text");

        builder.Property(r => r.BackTranslationC)
            .HasColumnType("text");

        builder.Property(r => r.BackTranslationD)
            .HasColumnType("text");

        // Scores
        builder.Property(r => r.ScoreA)
            .IsRequired()
            .HasDefaultValue(0);

        builder.Property(r => r.ScoreB)
            .IsRequired()
            .HasDefaultValue(0);

        builder.Property(r => r.ScoreC);
        builder.Property(r => r.ScoreD);

        builder.Property(r => r.FinalScore)
            .IsRequired()
            .HasDefaultValue(0);

        builder.Property(r => r.RoundsUsed)
            .IsRequired()
            .HasDefaultValue(1);

        builder.Property(r => r.Outcome)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(50);

        builder.Property(r => r.EngineOutcome)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(50);

        // Safety-critical metadata
        builder.Property(r => r.IsSafetyCritical)
            .IsRequired()
            .HasDefaultValue(false);

        builder.Property(r => r.CriticalTerms)
            .HasColumnType("text");

        builder.Property(r => r.GlossaryMismatches)
            .HasColumnType("text");

        builder.Property(r => r.EffectiveThreshold)
            .IsRequired();

        // Reviewer decision
        builder.Property(r => r.ReviewerDecision)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(50)
            .HasDefaultValue(ReviewerDecision.Pending);

        builder.Property(r => r.EditedTranslation)
            .HasColumnType("text");

        builder.Property(r => r.DecisionAt);

        builder.Property(r => r.DecisionBy)
            .HasMaxLength(256);

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
        builder.HasOne(r => r.ValidationRun)
            .WithMany(run => run.Results)
            .HasForeignKey(r => r.ValidationRunId)
            .OnDelete(DeleteBehavior.Cascade);

        // Indexes
        builder.HasIndex(r => r.ValidationRunId)
            .HasDatabaseName("ix_translation_validation_results_run");

        builder.HasIndex(r => new { r.ValidationRunId, r.SectionIndex })
            .IsUnique()
            .HasDatabaseName("ix_translation_validation_results_run_section");

        builder.HasIndex(r => r.Outcome)
            .HasDatabaseName("ix_translation_validation_results_outcome");

        builder.HasIndex(r => r.ReviewerDecision)
            .HasDatabaseName("ix_translation_validation_results_decision");

        // Query filter for soft delete
        builder.HasQueryFilter(r => !r.IsDeleted);
    }
}
