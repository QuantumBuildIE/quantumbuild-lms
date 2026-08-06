using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using QuantumBuild.Modules.ToolboxTalks.Domain.Entities;
using QuantumBuild.Modules.ToolboxTalks.Domain.Enums;

namespace QuantumBuild.Modules.ToolboxTalks.Infrastructure.Persistence.Configurations;

public class RegulatoryStructureMapConfiguration : IEntityTypeConfiguration<RegulatoryStructureMap>
{
    public void Configure(EntityTypeBuilder<RegulatoryStructureMap> builder)
    {
        builder.ToTable("RegulatoryStructureMaps", "toolbox_talks");
        builder.HasKey(e => e.Id);

        builder.Property(e => e.RegulatoryDocumentId)
            .IsRequired();

        builder.Property(e => e.Status)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(20)
            .HasDefaultValue(RegulatoryStructureMapStatus.Draft);

        builder.Property(e => e.VerifiedBy)
            .HasMaxLength(256);

        builder.Property(e => e.VerifiedAt);

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

        // Relationships
        builder.HasOne(e => e.RegulatoryDocument)
            .WithMany()
            .HasForeignKey(e => e.RegulatoryDocumentId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(e => e.Principles)
            .WithOne(p => p.RegulatoryStructureMap)
            .HasForeignKey(p => p.RegulatoryStructureMapId)
            .OnDelete(DeleteBehavior.Cascade);

        // Indexes — one map per document
        builder.HasIndex(e => e.RegulatoryDocumentId)
            .IsUnique()
            .HasDatabaseName("ix_regulatory_structure_maps_document");

        // Query filter for soft delete
        builder.HasQueryFilter(e => !e.IsDeleted);
    }
}
