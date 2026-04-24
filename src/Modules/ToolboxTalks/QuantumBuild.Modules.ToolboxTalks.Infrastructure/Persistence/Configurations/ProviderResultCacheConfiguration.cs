using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using QuantumBuild.Modules.ToolboxTalks.Domain.Entities;

namespace QuantumBuild.Modules.ToolboxTalks.Infrastructure.Persistence.Configurations;

public class ProviderResultCacheConfiguration : IEntityTypeConfiguration<ProviderResultCache>
{
    public void Configure(EntityTypeBuilder<ProviderResultCache> builder)
    {
        builder.ToTable("ProviderResultCache", "toolbox_talks");
        builder.HasKey(c => c.Id);

        builder.Property(c => c.Provider).IsRequired().HasMaxLength(50);
        builder.Property(c => c.ProviderVersion).IsRequired().HasMaxLength(100);
        builder.Property(c => c.BackTranslation).IsRequired().HasColumnType("text");

        builder.Property(c => c.CreatedAt).IsRequired();
        builder.Property(c => c.CreatedBy).IsRequired().HasMaxLength(256);
        builder.Property(c => c.UpdatedBy).HasMaxLength(256);
        builder.Property(c => c.IsDeleted).HasDefaultValue(false);

        // SetNull on delete — cache entries survive corpus entry removal for audit trail
        builder.HasOne(c => c.CorpusEntry)
            .WithMany()
            .HasForeignKey(c => c.CorpusEntryId)
            .OnDelete(DeleteBehavior.SetNull);

        // Unique index: (CorpusEntryId, Provider, ProviderVersion)
        builder.HasIndex(c => new { c.CorpusEntryId, c.Provider, c.ProviderVersion })
            .IsUnique()
            .HasDatabaseName("ix_provider_result_cache_entry_provider_version");

        builder.HasQueryFilter(c => !c.IsDeleted);
    }
}
