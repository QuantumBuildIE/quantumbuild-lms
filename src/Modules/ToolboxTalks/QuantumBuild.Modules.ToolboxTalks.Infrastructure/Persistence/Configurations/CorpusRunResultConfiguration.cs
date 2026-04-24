using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using QuantumBuild.Modules.ToolboxTalks.Domain.Entities;

namespace QuantumBuild.Modules.ToolboxTalks.Infrastructure.Persistence.Configurations;

public class CorpusRunResultConfiguration : IEntityTypeConfiguration<CorpusRunResult>
{
    public void Configure(EntityTypeBuilder<CorpusRunResult> builder)
    {
        builder.ToTable("CorpusRunResults", "toolbox_talks");
        builder.HasKey(r => r.Id);

        builder.Property(r => r.BackTranslationA).HasColumnType("text");
        builder.Property(r => r.BackTranslationB).HasColumnType("text");
        builder.Property(r => r.BackTranslationC).HasColumnType("text");
        builder.Property(r => r.BackTranslationD).HasColumnType("text");
        builder.Property(r => r.GlossaryCorrectionsJson).HasColumnType("text");
        builder.Property(r => r.ArtefactsJson).HasColumnType("text");
        builder.Property(r => r.ReviewReasonsJson).HasColumnType("text");
        builder.Property(r => r.WasCached).HasDefaultValue(false);

        builder.Property(r => r.Outcome)
            .HasConversion<string>()
            .IsRequired();

        builder.Property(r => r.ExpectedOutcome)
            .HasConversion<string>()
            .IsRequired();

        builder.Property(r => r.CreatedAt).IsRequired();
        builder.Property(r => r.CreatedBy).IsRequired().HasMaxLength(256);
        builder.Property(r => r.UpdatedBy).HasMaxLength(256);
        builder.Property(r => r.IsDeleted).HasDefaultValue(false);

        builder.HasOne(r => r.CorpusRun)
            .WithMany(run => run.Results)
            .HasForeignKey(r => r.CorpusRunId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(r => r.CorpusEntry)
            .WithMany()
            .HasForeignKey(r => r.CorpusEntryId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(r => r.CorpusRunId)
            .HasDatabaseName("ix_corpus_run_results_corpus_run_id");

        builder.HasQueryFilter(r => !r.IsDeleted);
    }
}
