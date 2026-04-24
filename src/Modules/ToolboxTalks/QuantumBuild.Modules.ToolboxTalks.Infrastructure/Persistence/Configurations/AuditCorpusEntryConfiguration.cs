using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using QuantumBuild.Modules.ToolboxTalks.Domain.Entities;
using QuantumBuild.Modules.ToolboxTalks.Domain.Enums;

namespace QuantumBuild.Modules.ToolboxTalks.Infrastructure.Persistence.Configurations;

public class AuditCorpusEntryConfiguration : IEntityTypeConfiguration<AuditCorpusEntry>
{
    public void Configure(EntityTypeBuilder<AuditCorpusEntry> builder)
    {
        builder.ToTable("AuditCorpusEntries", "toolbox_talks");
        builder.HasKey(e => e.Id);

        builder.Property(e => e.EntryRef).IsRequired().HasMaxLength(50);
        builder.Property(e => e.SectionTitle).IsRequired().HasMaxLength(500);
        builder.Property(e => e.OriginalText).IsRequired().HasColumnType("text");
        builder.Property(e => e.TranslatedText).IsRequired().HasColumnType("text");
        builder.Property(e => e.SourceLanguage).IsRequired().HasMaxLength(10);
        builder.Property(e => e.TargetLanguage).IsRequired().HasMaxLength(10);
        builder.Property(e => e.SectorKey).IsRequired().HasMaxLength(50);
        builder.Property(e => e.TagsJson).HasColumnType("text");
        builder.Property(e => e.IsActive).HasDefaultValue(true);

        builder.Property(e => e.ExpectedOutcome)
            .HasConversion<string>()
            .IsRequired();

        builder.Property(e => e.CreatedAt).IsRequired();
        builder.Property(e => e.CreatedBy).IsRequired().HasMaxLength(256);
        builder.Property(e => e.UpdatedBy).HasMaxLength(256);
        builder.Property(e => e.IsDeleted).HasDefaultValue(false);

        builder.HasOne(e => e.Corpus)
            .WithMany(c => c.Entries)
            .HasForeignKey(e => e.CorpusId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(e => e.CorpusId)
            .HasDatabaseName("ix_audit_corpus_entries_corpus_id");

        builder.HasQueryFilter(e => !e.IsDeleted);
    }
}
