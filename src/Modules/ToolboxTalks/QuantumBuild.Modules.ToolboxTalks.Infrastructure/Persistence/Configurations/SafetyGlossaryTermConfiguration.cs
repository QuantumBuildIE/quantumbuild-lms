using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using QuantumBuild.Modules.ToolboxTalks.Domain.Entities;

namespace QuantumBuild.Modules.ToolboxTalks.Infrastructure.Persistence.Configurations;

public class SafetyGlossaryTermConfiguration : IEntityTypeConfiguration<SafetyGlossaryTerm>
{
    public void Configure(EntityTypeBuilder<SafetyGlossaryTerm> builder)
    {
        builder.ToTable("SafetyGlossaryTerms", "toolbox_talks");
        builder.HasKey(t => t.Id);

        builder.Property(t => t.GlossaryId)
            .IsRequired();

        builder.Property(t => t.EnglishTerm)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(t => t.Category)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(t => t.IsCritical)
            .IsRequired()
            .HasDefaultValue(true);

        builder.Property(t => t.Translations)
            .IsRequired()
            .HasColumnType("text")
            .HasDefaultValue("{}");

        // Audit fields
        builder.Property(t => t.CreatedAt)
            .IsRequired();

        builder.Property(t => t.CreatedBy)
            .IsRequired()
            .HasMaxLength(256);

        builder.Property(t => t.UpdatedAt);

        builder.Property(t => t.UpdatedBy)
            .HasMaxLength(256);

        builder.Property(t => t.IsDeleted)
            .IsRequired()
            .HasDefaultValue(false);

        // Relationships
        builder.HasOne(t => t.Glossary)
            .WithMany(g => g.Terms)
            .HasForeignKey(t => t.GlossaryId)
            .OnDelete(DeleteBehavior.Cascade);

        // Indexes
        builder.HasIndex(t => t.GlossaryId)
            .HasDatabaseName("ix_safety_glossary_terms_glossary");

        builder.HasIndex(t => new { t.GlossaryId, t.EnglishTerm })
            .IsUnique()
            .HasDatabaseName("ix_safety_glossary_terms_glossary_term");

        builder.HasIndex(t => t.Category)
            .HasDatabaseName("ix_safety_glossary_terms_category");

        // Query filter for soft delete
        builder.HasQueryFilter(t => !t.IsDeleted);
    }
}
