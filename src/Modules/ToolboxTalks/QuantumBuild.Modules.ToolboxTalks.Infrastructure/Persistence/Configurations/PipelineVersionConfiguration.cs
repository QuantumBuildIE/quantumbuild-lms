using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using QuantumBuild.Modules.ToolboxTalks.Domain.Entities;

namespace QuantumBuild.Modules.ToolboxTalks.Infrastructure.Persistence.Configurations;

public class PipelineVersionConfiguration : IEntityTypeConfiguration<PipelineVersion>
{
    public void Configure(EntityTypeBuilder<PipelineVersion> builder)
    {
        builder.ToTable("PipelineVersions", "toolbox_talks");
        builder.HasKey(v => v.Id);

        builder.Property(v => v.Version)
            .IsRequired()
            .HasMaxLength(20);

        builder.Property(v => v.Hash)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(v => v.ComponentsJson)
            .IsRequired()
            .HasColumnType("text");

        builder.Property(v => v.ComputedAt)
            .IsRequired();

        builder.Property(v => v.IsActive)
            .IsRequired()
            .HasDefaultValue(false);

        // Audit fields
        builder.Property(v => v.CreatedAt)
            .IsRequired();

        builder.Property(v => v.CreatedBy)
            .IsRequired()
            .HasMaxLength(256);

        builder.Property(v => v.UpdatedAt);

        builder.Property(v => v.UpdatedBy)
            .HasMaxLength(256);

        builder.Property(v => v.IsDeleted)
            .IsRequired()
            .HasDefaultValue(false);

        // Relationships
        builder.HasMany(v => v.Runs)
            .WithOne(r => r.PipelineVersion)
            .HasForeignKey(r => r.PipelineVersionId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasMany(v => v.ChangeRecords)
            .WithOne(cr => cr.PipelineVersion)
            .HasForeignKey(cr => cr.PipelineVersionId)
            .OnDelete(DeleteBehavior.Restrict);

        // Indexes — Hash is unique; IsActive is indexed for fast active-record lookup
        builder.HasIndex(v => v.Hash)
            .IsUnique()
            .HasDatabaseName("ix_pipeline_versions_hash");

        builder.HasIndex(v => v.IsActive)
            .HasDatabaseName("ix_pipeline_versions_is_active");

        // Soft-delete query filter
        builder.HasQueryFilter(v => !v.IsDeleted);
    }
}
