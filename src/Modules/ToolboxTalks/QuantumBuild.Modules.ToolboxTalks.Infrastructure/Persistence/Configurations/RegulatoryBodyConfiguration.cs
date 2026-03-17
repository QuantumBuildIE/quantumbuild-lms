using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using QuantumBuild.Modules.ToolboxTalks.Domain.Entities;

namespace QuantumBuild.Modules.ToolboxTalks.Infrastructure.Persistence.Configurations;

public class RegulatoryBodyConfiguration : IEntityTypeConfiguration<RegulatoryBody>
{
    public void Configure(EntityTypeBuilder<RegulatoryBody> builder)
    {
        builder.ToTable("RegulatoryBodies", "toolbox_talks");
        builder.HasKey(e => e.Id);

        builder.Property(e => e.Name)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(e => e.Code)
            .IsRequired()
            .HasMaxLength(20);

        builder.Property(e => e.Country)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(e => e.Website)
            .HasMaxLength(500);

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
        builder.HasMany(e => e.Documents)
            .WithOne(d => d.RegulatoryBody)
            .HasForeignKey(d => d.RegulatoryBodyId)
            .OnDelete(DeleteBehavior.Restrict);

        // Indexes
        builder.HasIndex(e => e.Code)
            .IsUnique()
            .HasDatabaseName("ix_regulatory_bodies_code");

        // Query filter for soft delete
        builder.HasQueryFilter(e => !e.IsDeleted);
    }
}
