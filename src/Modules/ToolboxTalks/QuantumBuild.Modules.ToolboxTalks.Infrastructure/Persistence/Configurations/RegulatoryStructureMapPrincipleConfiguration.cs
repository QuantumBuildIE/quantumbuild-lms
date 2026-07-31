using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using QuantumBuild.Modules.ToolboxTalks.Domain.Entities;

namespace QuantumBuild.Modules.ToolboxTalks.Infrastructure.Persistence.Configurations;

public class RegulatoryStructureMapPrincipleConfiguration : IEntityTypeConfiguration<RegulatoryStructureMapPrinciple>
{
    public void Configure(EntityTypeBuilder<RegulatoryStructureMapPrinciple> builder)
    {
        builder.ToTable("RegulatoryStructureMapPrinciples", "toolbox_talks");
        builder.HasKey(e => e.Id);

        builder.Property(e => e.RegulatoryStructureMapId)
            .IsRequired();

        builder.Property(e => e.Number)
            .IsRequired();

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
        builder.HasMany(e => e.Standards)
            .WithOne(s => s.RegulatoryStructureMapPrinciple)
            .HasForeignKey(s => s.RegulatoryStructureMapPrincipleId)
            .OnDelete(DeleteBehavior.Cascade);

        // Indexes
        builder.HasIndex(e => new { e.RegulatoryStructureMapId, e.Number })
            .IsUnique()
            .HasDatabaseName("ix_regulatory_structure_map_principles_map_number");

        // Query filter for soft delete
        builder.HasQueryFilter(e => !e.IsDeleted);
    }
}
