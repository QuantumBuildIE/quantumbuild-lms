using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using QuantumBuild.Modules.ToolboxTalks.Domain.Entities;

namespace QuantumBuild.Modules.ToolboxTalks.Infrastructure.Persistence.Configurations;

public class CustomerUsageReportStateConfiguration : IEntityTypeConfiguration<CustomerUsageReportState>
{
    public void Configure(EntityTypeBuilder<CustomerUsageReportState> builder)
    {
        builder.ToTable("CustomerUsageReportStates", "toolbox_talks");
        builder.HasKey(e => e.Id);

        builder.Property(e => e.LastReviewedAt);

        builder.Property(e => e.CreatedAt).IsRequired();
        builder.Property(e => e.CreatedBy).IsRequired().HasMaxLength(256);
        builder.Property(e => e.UpdatedAt);
        builder.Property(e => e.UpdatedBy).HasMaxLength(256);
        builder.Property(e => e.IsDeleted).IsRequired().HasDefaultValue(false);
        builder.Property(e => e.DeletedBy).HasMaxLength(256);

        // Soft-delete filter only — no TenantId (system-level, like PipelineVersion)
        builder.HasQueryFilter(e => !e.IsDeleted);
    }
}
