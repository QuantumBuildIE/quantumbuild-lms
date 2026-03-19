using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using QuantumBuild.Modules.ToolboxTalks.Domain.Entities;
using QuantumBuild.Modules.ToolboxTalks.Domain.Enums;

namespace QuantumBuild.Modules.ToolboxTalks.Infrastructure.Persistence.Configurations;

public class RegulatoryRequirementMappingConfiguration : IEntityTypeConfiguration<RegulatoryRequirementMapping>
{
    public void Configure(EntityTypeBuilder<RegulatoryRequirementMapping> builder)
    {
        builder.ToTable("RegulatoryRequirementMappings", "toolbox_talks", t => t.HasCheckConstraint(
            "ck_regulatory_requirement_mappings_talk_or_course",
            "(\"ToolboxTalkId\" IS NOT NULL AND \"CourseId\" IS NULL) OR (\"ToolboxTalkId\" IS NULL AND \"CourseId\" IS NOT NULL)"));
        builder.HasKey(e => e.Id);

        builder.Property(e => e.RegulatoryRequirementId)
            .IsRequired();

        builder.Property(e => e.ToolboxTalkId);

        builder.Property(e => e.CourseId);

        builder.Property(e => e.MappingStatus)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(50);

        builder.Property(e => e.ConfidenceScore);

        builder.Property(e => e.AiReasoning)
            .HasMaxLength(2000);

        builder.Property(e => e.ReviewedBy)
            .HasMaxLength(256);

        builder.Property(e => e.ReviewedAt);

        builder.Property(e => e.ReviewNotes)
            .HasMaxLength(1000);

        builder.Property(e => e.TenantId)
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
        // FK to RegulatoryRequirement (configured via HasMany on RegulatoryRequirement)
        // Nullable FK to ToolboxTalk
        builder.HasOne(e => e.ToolboxTalk)
            .WithMany()
            .HasForeignKey(e => e.ToolboxTalkId)
            .OnDelete(DeleteBehavior.Restrict);

        // Nullable FK to ToolboxTalkCourse
        builder.HasOne(e => e.Course)
            .WithMany()
            .HasForeignKey(e => e.CourseId)
            .OnDelete(DeleteBehavior.Restrict);

        // Cross-module FK to Tenant (no navigation property on Tenant side)
        builder.HasOne<QuantumBuild.Core.Domain.Entities.Tenant>()
            .WithMany()
            .HasForeignKey(e => e.TenantId)
            .OnDelete(DeleteBehavior.Restrict);

        // Indexes — composite unique to prevent duplicate mappings
        builder.HasIndex(e => new { e.TenantId, e.RegulatoryRequirementId, e.ToolboxTalkId })
            .IsUnique()
            .HasFilter("\"ToolboxTalkId\" IS NOT NULL")
            .HasDatabaseName("ix_regulatory_requirement_mappings_tenant_req_talk");

        builder.HasIndex(e => new { e.TenantId, e.RegulatoryRequirementId, e.CourseId })
            .IsUnique()
            .HasFilter("\"CourseId\" IS NOT NULL")
            .HasDatabaseName("ix_regulatory_requirement_mappings_tenant_req_course");

        builder.HasIndex(e => new { e.TenantId, e.MappingStatus })
            .HasDatabaseName("ix_regulatory_requirement_mappings_tenant_status");

        // Query filter for soft delete (tenant filter applied in ApplicationDbContext)
        builder.HasQueryFilter(e => !e.IsDeleted);
    }
}
