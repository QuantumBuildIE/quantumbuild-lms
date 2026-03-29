using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using QuantumBuild.Modules.ToolboxTalks.Domain.Entities;
using QuantumBuild.Modules.ToolboxTalks.Domain.Enums;

namespace QuantumBuild.Modules.ToolboxTalks.Infrastructure.Persistence.Configurations;

public class AiUsageSummaryConfiguration : IEntityTypeConfiguration<AiUsageSummary>
{
    public void Configure(EntityTypeBuilder<AiUsageSummary> builder)
    {
        builder.ToTable("AiUsageSummaries", "toolbox_talks");
        builder.HasKey(e => e.Id);

        // Tenant
        builder.Property(e => e.TenantId)
            .IsRequired();

        builder.Property(e => e.Date)
            .IsRequired();

        builder.Property(e => e.OperationCategory)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(50);

        builder.Property(e => e.ModelId)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(e => e.TotalCalls)
            .IsRequired();

        builder.Property(e => e.TotalInputTokens)
            .IsRequired();

        builder.Property(e => e.TotalOutputTokens)
            .IsRequired();

        builder.Property(e => e.SystemCallCount)
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

        // Indexes
        builder.HasIndex(e => new { e.TenantId, e.Date, e.OperationCategory, e.ModelId })
            .IsUnique()
            .HasDatabaseName("ix_ai_usage_summaries_tenant_date_category_model");

        // Query filter for soft delete
        builder.HasQueryFilter(e => !e.IsDeleted);
    }
}
