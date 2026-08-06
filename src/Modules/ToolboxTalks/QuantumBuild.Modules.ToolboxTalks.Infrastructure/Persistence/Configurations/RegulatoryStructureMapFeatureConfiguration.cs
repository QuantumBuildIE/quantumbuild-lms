using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using QuantumBuild.Modules.ToolboxTalks.Domain.Entities;

namespace QuantumBuild.Modules.ToolboxTalks.Infrastructure.Persistence.Configurations;

public class RegulatoryStructureMapFeatureConfiguration : IEntityTypeConfiguration<RegulatoryStructureMapFeature>
{
    public void Configure(EntityTypeBuilder<RegulatoryStructureMapFeature> builder)
    {
        builder.ToTable("RegulatoryStructureMapFeatures", "toolbox_talks");
        builder.HasKey(e => e.Id);

        builder.Property(e => e.RegulatoryStructureMapStandardId)
            .IsRequired();

        builder.Property(e => e.Identifier)
            .IsRequired()
            .HasMaxLength(20);

        builder.Property(e => e.Block)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(50);

        builder.Property(e => e.VerbatimText)
            .IsRequired()
            .HasMaxLength(2000);

        builder.Property(e => e.FootnoteDefinition)
            .HasMaxLength(1000);

        builder.Property(e => e.DisplayOrder)
            .IsRequired();

        // Audit fields
        builder.Property(e => e.CreatedAt)
            .IsRequired();

        builder.Property(e => e.CreatedBy)
            .IsRequired()
            .HasMaxLength(256);

        builder.Property(e => e.UpdatedAt);

        builder.Property(e => e.UpdatedBy)
            .HasMaxLength(256);

        builder.Property(e => e.IsDeleted)
            .IsRequired()
            .HasDefaultValue(false);

        // Indexes — Identifier is unique per standard per block (the two blocks are
        // independently numbered, so Block must be part of the key)
        builder.HasIndex(e => new { e.RegulatoryStructureMapStandardId, e.Block, e.Identifier })
            .IsUnique()
            .HasDatabaseName("ix_regulatory_structure_map_features_standard_block_identifier");

        // Query filter for soft delete
        builder.HasQueryFilter(e => !e.IsDeleted);
    }
}
