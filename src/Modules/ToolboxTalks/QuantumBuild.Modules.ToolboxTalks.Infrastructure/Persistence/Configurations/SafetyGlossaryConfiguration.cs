using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using QuantumBuild.Modules.ToolboxTalks.Domain.Entities;

namespace QuantumBuild.Modules.ToolboxTalks.Infrastructure.Persistence.Configurations;

public class SafetyGlossaryConfiguration : IEntityTypeConfiguration<SafetyGlossary>
{
    public void Configure(EntityTypeBuilder<SafetyGlossary> builder)
    {
        builder.ToTable("SafetyGlossaries", "toolbox_talks");
        builder.HasKey(g => g.Id);

        // Nullable TenantId: null = system default, Guid = tenant override
        builder.Property(g => g.TenantId);

        builder.Property(g => g.SectorKey)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(g => g.SectorName)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(g => g.SectorIcon)
            .HasMaxLength(50);

        builder.Property(g => g.IsActive)
            .IsRequired()
            .HasDefaultValue(true);

        // Audit fields
        builder.Property(g => g.CreatedAt)
            .IsRequired();

        builder.Property(g => g.CreatedBy)
            .IsRequired()
            .HasMaxLength(256);

        builder.Property(g => g.UpdatedAt);

        builder.Property(g => g.UpdatedBy)
            .HasMaxLength(256);

        builder.Property(g => g.IsDeleted)
            .IsRequired()
            .HasDefaultValue(false);

        // Relationships
        builder.HasMany(g => g.Terms)
            .WithOne(t => t.Glossary)
            .HasForeignKey(t => t.GlossaryId)
            .OnDelete(DeleteBehavior.Cascade);

        // Indexes
        builder.HasIndex(g => g.TenantId)
            .HasDatabaseName("ix_safety_glossaries_tenant");

        builder.HasIndex(g => new { g.TenantId, g.SectorKey })
            .IsUnique()
            .HasDatabaseName("ix_safety_glossaries_tenant_sector");

        // Query filter for soft delete
        builder.HasQueryFilter(g => !g.IsDeleted);
    }
}
