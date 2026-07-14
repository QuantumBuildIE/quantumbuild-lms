using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using QuantumBuild.Modules.ToolboxTalks.Domain.Entities.Workflows;
using QuantumBuild.Modules.ToolboxTalks.Domain.Enums;

namespace QuantumBuild.Modules.ToolboxTalks.Infrastructure.Persistence.Configurations.Workflows;

public class WorkflowEventConfiguration : IEntityTypeConfiguration<WorkflowEvent>
{
    public void Configure(EntityTypeBuilder<WorkflowEvent> builder)
    {
        builder.ToTable("WorkflowEvents", "workflows");
        builder.HasKey(e => e.Id);

        builder.Property(e => e.WorkflowType)
            .IsRequired()
            .HasConversion<int>();

        builder.Property(e => e.TargetEntityId)
            .IsRequired();

        builder.Property(e => e.TargetEntitySubKey)
            .HasMaxLength(32);

        builder.Property(e => e.EventType)
            .IsRequired()
            .HasMaxLength(64);

        builder.Property(e => e.TriggeredByType)
            .IsRequired()
            .HasConversion<int>();

        builder.Property(e => e.TriggeredByUserId);

        builder.Property(e => e.PayloadJson)
            .HasColumnType("jsonb");

        builder.Property(e => e.OccurredAt)
            .IsRequired();

        builder.Property(e => e.TenantId)
            .IsRequired();

        builder.Property(e => e.CreatedAt).IsRequired();
        builder.Property(e => e.CreatedBy).IsRequired().HasMaxLength(256);
        builder.Property(e => e.UpdatedAt);
        builder.Property(e => e.UpdatedBy).HasMaxLength(256);
        builder.Property(e => e.IsDeleted).IsRequired().HasDefaultValue(false);

        builder.HasIndex(e => new { e.WorkflowType, e.TargetEntityId, e.TargetEntitySubKey })
            .HasDatabaseName("ix_workflow_events_target");

        builder.HasIndex(e => e.TenantId)
            .HasDatabaseName("ix_workflow_events_tenant");
    }
}
