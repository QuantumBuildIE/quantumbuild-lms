using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using QuantumBuild.Modules.ToolboxTalks.Domain.Entities.Workflows;
using QuantumBuild.Modules.ToolboxTalks.Domain.Enums;

namespace QuantumBuild.Modules.ToolboxTalks.Infrastructure.Persistence.Configurations.Workflows;

public class WorkflowReviewConfiguration : IEntityTypeConfiguration<WorkflowReview>
{
    public void Configure(EntityTypeBuilder<WorkflowReview> builder)
    {
        builder.ToTable("WorkflowReviews", "workflows");
        builder.HasKey(e => e.Id);

        builder.Property(e => e.WorkflowType)
            .IsRequired()
            .HasConversion<int>();

        builder.Property(e => e.TargetEntityId)
            .IsRequired();

        builder.Property(e => e.TargetEntitySubKey)
            .HasMaxLength(32);

        builder.Property(e => e.ReviewerType)
            .IsRequired()
            .HasConversion<int>();

        builder.Property(e => e.ReviewerUserId);

        builder.Property(e => e.ExternalParticipantInvitationId);

        builder.Property(e => e.EditedContent)
            .HasColumnType("text");

        builder.Property(e => e.Accepted)
            .IsRequired();

        builder.Property(e => e.SubmittedAt)
            .IsRequired();

        builder.Property(e => e.TenantId)
            .IsRequired();

        builder.Property(e => e.CreatedAt).IsRequired();
        builder.Property(e => e.CreatedBy).IsRequired().HasMaxLength(256);
        builder.Property(e => e.UpdatedAt);
        builder.Property(e => e.UpdatedBy).HasMaxLength(256);
        builder.Property(e => e.IsDeleted).IsRequired().HasDefaultValue(false);

        builder.HasOne(e => e.ExternalParticipantInvitation)
            .WithMany()
            .HasForeignKey(e => e.ExternalParticipantInvitationId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasIndex(e => new { e.WorkflowType, e.TargetEntityId, e.TargetEntitySubKey })
            .HasDatabaseName("ix_workflow_reviews_target");

        builder.HasIndex(e => e.TenantId)
            .HasDatabaseName("ix_workflow_reviews_tenant");
    }
}
