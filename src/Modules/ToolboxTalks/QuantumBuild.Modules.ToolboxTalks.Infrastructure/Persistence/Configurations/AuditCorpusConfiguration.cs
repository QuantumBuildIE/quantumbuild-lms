using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using QuantumBuild.Modules.ToolboxTalks.Domain.Entities;

namespace QuantumBuild.Modules.ToolboxTalks.Infrastructure.Persistence.Configurations;

public class AuditCorpusConfiguration : IEntityTypeConfiguration<AuditCorpus>
{
    public void Configure(EntityTypeBuilder<AuditCorpus> builder)
    {
        builder.ToTable("AuditCorpora", "toolbox_talks");
        builder.HasKey(c => c.Id);

        builder.Property(c => c.CorpusId).IsRequired().HasMaxLength(30);
        builder.Property(c => c.Name).IsRequired().HasMaxLength(200);
        builder.Property(c => c.Description).HasColumnType("text");
        builder.Property(c => c.SectorKey).IsRequired().HasMaxLength(50);
        builder.Property(c => c.LanguagePair).IsRequired().HasMaxLength(20);
        builder.Property(c => c.LockedBy).HasMaxLength(256);
        builder.Property(c => c.SignedBy).HasMaxLength(256);
        builder.Property(c => c.Version).HasDefaultValue(1);
        builder.Property(c => c.IsLocked).HasDefaultValue(false);

        builder.Property(c => c.CreatedAt).IsRequired();
        builder.Property(c => c.CreatedBy).IsRequired().HasMaxLength(256);
        builder.Property(c => c.UpdatedBy).HasMaxLength(256);
        builder.Property(c => c.IsDeleted).HasDefaultValue(false);

        // FK — SetNull so corpus survives talk deletion
        builder.HasOne(c => c.SourceTalk)
            .WithMany()
            .HasForeignKey(c => c.SourceTalkId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne(c => c.FrozenFromPipelineVersion)
            .WithMany()
            .HasForeignKey(c => c.FrozenFromPipelineVersionId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasMany(c => c.Entries)
            .WithOne(e => e.Corpus)
            .HasForeignKey(e => e.CorpusId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(c => c.Runs)
            .WithOne(r => r.AuditCorpus)
            .HasForeignKey(r => r.CorpusId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(c => new { c.TenantId, c.CorpusId })
            .IsUnique()
            .HasDatabaseName("ix_audit_corpora_tenant_id_corpus_id");

        builder.HasQueryFilter(c => !c.IsDeleted);
    }
}
