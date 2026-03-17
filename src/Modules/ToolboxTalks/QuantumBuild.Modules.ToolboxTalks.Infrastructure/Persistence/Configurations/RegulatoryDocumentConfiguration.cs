using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using QuantumBuild.Modules.ToolboxTalks.Domain.Entities;

namespace QuantumBuild.Modules.ToolboxTalks.Infrastructure.Persistence.Configurations;

public class RegulatoryDocumentConfiguration : IEntityTypeConfiguration<RegulatoryDocument>
{
    public void Configure(EntityTypeBuilder<RegulatoryDocument> builder)
    {
        builder.ToTable("RegulatoryDocuments", "toolbox_talks");
        builder.HasKey(e => e.Id);

        builder.Property(e => e.RegulatoryBodyId)
            .IsRequired();

        builder.Property(e => e.Title)
            .IsRequired()
            .HasMaxLength(500);

        builder.Property(e => e.Version)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(e => e.EffectiveDate);

        builder.Property(e => e.Source)
            .HasMaxLength(200);

        builder.Property(e => e.SourceUrl)
            .HasMaxLength(2000);

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

        // Relationships (configured via RegulatoryBody's HasMany)
        builder.HasMany(e => e.Profiles)
            .WithOne(p => p.RegulatoryDocument)
            .HasForeignKey(p => p.RegulatoryDocumentId)
            .OnDelete(DeleteBehavior.Restrict);

        // Indexes
        builder.HasIndex(e => e.RegulatoryBodyId)
            .HasDatabaseName("ix_regulatory_documents_body");

        // Query filter for soft delete
        builder.HasQueryFilter(e => !e.IsDeleted);
    }
}
