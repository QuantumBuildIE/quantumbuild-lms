using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using QuantumBuild.Modules.ToolboxTalks.Domain.Entities;

namespace QuantumBuild.Modules.ToolboxTalks.Infrastructure.Persistence.Configurations;

public class RegulatoryProfileConfiguration : IEntityTypeConfiguration<RegulatoryProfile>
{
    public void Configure(EntityTypeBuilder<RegulatoryProfile> builder)
    {
        builder.ToTable("RegulatoryProfiles", "toolbox_talks");
        builder.HasKey(e => e.Id);

        builder.Property(e => e.RegulatoryDocumentId)
            .IsRequired();

        builder.Property(e => e.SectorId)
            .IsRequired();

        builder.Property(e => e.SectorKey)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(e => e.ScoreLabel)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(e => e.ExportLabel)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(e => e.Description)
            .IsRequired()
            .HasMaxLength(500);

        builder.Property(e => e.CategoryWeightsJson)
            .IsRequired()
            .HasColumnType("text");

        builder.Property(e => e.IsActive)
            .IsRequired()
            .HasDefaultValue(true);

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
        builder.HasOne(e => e.Sector)
            .WithMany()
            .HasForeignKey(e => e.SectorId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(e => e.Criteria)
            .WithOne(c => c.RegulatoryProfile)
            .HasForeignKey(c => c.RegulatoryProfileId)
            .OnDelete(DeleteBehavior.Restrict);

        // Indexes
        builder.HasIndex(e => new { e.RegulatoryDocumentId, e.SectorId })
            .IsUnique()
            .HasDatabaseName("ix_regulatory_profiles_document_sector");

        builder.HasIndex(e => e.SectorKey)
            .HasDatabaseName("ix_regulatory_profiles_sector_key");

        // Query filter for soft delete
        builder.HasQueryFilter(e => !e.IsDeleted);
    }
}
