using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using QuantumBuild.Modules.ToolboxTalks.Domain.Entities;
using QuantumBuild.Modules.ToolboxTalks.Domain.Enums;

namespace QuantumBuild.Modules.ToolboxTalks.Infrastructure.Persistence.Configurations;

public class AiUsageLogConfiguration : IEntityTypeConfiguration<AiUsageLog>
{
    public void Configure(EntityTypeBuilder<AiUsageLog> builder)
    {
        builder.ToTable("AiUsageLogs", "toolbox_talks");
        builder.HasKey(e => e.Id);

        // Tenant
        builder.Property(e => e.TenantId)
            .IsRequired();

        builder.Property(e => e.UserId);

        builder.Property(e => e.OperationCategory)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(50);

        builder.Property(e => e.ModelId)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(e => e.InputTokens)
            .IsRequired();

        builder.Property(e => e.OutputTokens)
            .IsRequired();

        builder.Property(e => e.CalledAt)
            .IsRequired();

        builder.Property(e => e.IsSystemCall)
            .IsRequired()
            .HasDefaultValue(false);

        builder.Property(e => e.ReferenceEntityId);

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
        builder.HasIndex(e => e.TenantId)
            .HasDatabaseName("ix_ai_usage_logs_tenant");

        builder.HasIndex(e => new { e.TenantId, e.CalledAt })
            .HasDatabaseName("ix_ai_usage_logs_tenant_called_at");

        // Query filter for soft delete
        builder.HasQueryFilter(e => !e.IsDeleted);
    }
}
