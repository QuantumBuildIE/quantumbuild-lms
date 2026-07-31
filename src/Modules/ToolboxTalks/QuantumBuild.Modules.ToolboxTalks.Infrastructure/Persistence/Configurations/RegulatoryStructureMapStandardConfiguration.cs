using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using QuantumBuild.Modules.ToolboxTalks.Domain.Entities;

namespace QuantumBuild.Modules.ToolboxTalks.Infrastructure.Persistence.Configurations;

public class RegulatoryStructureMapStandardConfiguration : IEntityTypeConfiguration<RegulatoryStructureMapStandard>
{
    public void Configure(EntityTypeBuilder<RegulatoryStructureMapStandard> builder)
    {
        builder.ToTable("RegulatoryStructureMapStandards", "toolbox_talks");
        builder.HasKey(e => e.Id);

        builder.Property(e => e.RegulatoryStructureMapPrincipleId)
            .IsRequired();

        builder.Property(e => e.StandardId)
            .IsRequired()
            .HasMaxLength(20);

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

        // Relationships
        builder.HasMany(e => e.Features)
            .WithOne(f => f.RegulatoryStructureMapStandard)
            .HasForeignKey(f => f.RegulatoryStructureMapStandardId)
            .OnDelete(DeleteBehavior.Cascade);

        // Indexes
        builder.HasIndex(e => new { e.RegulatoryStructureMapPrincipleId, e.StandardId })
            .IsUnique()
            .HasDatabaseName("ix_regulatory_structure_map_standards_principle_standard");

        // Query filter for soft delete
        builder.HasQueryFilter(e => !e.IsDeleted);
    }
}
