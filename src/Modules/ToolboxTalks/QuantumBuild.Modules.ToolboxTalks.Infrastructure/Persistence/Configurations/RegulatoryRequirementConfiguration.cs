using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using QuantumBuild.Modules.ToolboxTalks.Domain.Entities;
using QuantumBuild.Modules.ToolboxTalks.Domain.Enums;

namespace QuantumBuild.Modules.ToolboxTalks.Infrastructure.Persistence.Configurations;

public class RegulatoryRequirementConfiguration : IEntityTypeConfiguration<RegulatoryRequirement>
{
    public void Configure(EntityTypeBuilder<RegulatoryRequirement> builder)
    {
        builder.ToTable("RegulatoryRequirements", "toolbox_talks");
        builder.HasKey(e => e.Id);

        builder.Property(e => e.RegulatoryProfileId)
            .IsRequired();

        builder.Property(e => e.Title)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(e => e.Description)
            .IsRequired()
            .HasMaxLength(2000);

        builder.Property(e => e.Section)
            .HasMaxLength(20);

        builder.Property(e => e.SectionLabel)
            .HasMaxLength(200);

        builder.Property(e => e.Principle)
            .HasMaxLength(20);

        builder.Property(e => e.PrincipleLabel)
            .HasMaxLength(200);

        builder.Property(e => e.Priority)
            .IsRequired()
            .HasMaxLength(20);

        builder.Property(e => e.DisplayOrder)
            .IsRequired();

        builder.Property(e => e.IngestionSource)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(50);

        builder.Property(e => e.IngestionStatus)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(50);

        builder.Property(e => e.IngestionNotes)
            .HasMaxLength(1000);

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
        builder.HasOne(e => e.RegulatoryProfile)
            .WithMany()
            .HasForeignKey(e => e.RegulatoryProfileId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(e => e.Mappings)
            .WithOne(m => m.RegulatoryRequirement)
            .HasForeignKey(m => m.RegulatoryRequirementId)
            .OnDelete(DeleteBehavior.Restrict);

        // Indexes
        builder.HasIndex(e => e.RegulatoryProfileId)
            .HasDatabaseName("ix_regulatory_requirements_profile");

        builder.HasIndex(e => e.IngestionStatus)
            .HasDatabaseName("ix_regulatory_requirements_ingestion_status");

        // Query filter for soft delete
        builder.HasQueryFilter(e => !e.IsDeleted);
    }
}
