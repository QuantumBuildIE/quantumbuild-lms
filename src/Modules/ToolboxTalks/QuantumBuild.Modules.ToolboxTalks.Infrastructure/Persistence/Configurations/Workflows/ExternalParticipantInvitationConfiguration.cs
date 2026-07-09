using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using QuantumBuild.Modules.ToolboxTalks.Domain.Entities.Workflows;
using QuantumBuild.Modules.ToolboxTalks.Domain.Enums;

namespace QuantumBuild.Modules.ToolboxTalks.Infrastructure.Persistence.Configurations.Workflows;

public class ExternalParticipantInvitationConfiguration : IEntityTypeConfiguration<ExternalParticipantInvitation>
{
    public void Configure(EntityTypeBuilder<ExternalParticipantInvitation> builder)
    {
        builder.ToTable("ExternalParticipantInvitations", "workflows");
        builder.HasKey(e => e.Id);

        builder.Property(e => e.WorkflowType)
            .IsRequired()
            .HasConversion<int>();

        builder.Property(e => e.TargetEntityId)
            .IsRequired();

        builder.Property(e => e.TargetEntitySubKey)
            .HasMaxLength(32);

        builder.Property(e => e.InvitedEmail)
            .IsRequired()
            .HasMaxLength(256);

        builder.Property(e => e.TokenHash)
            .IsRequired()
            .HasMaxLength(128);

        builder.Property(e => e.ExpiresAt)
            .IsRequired();

        builder.Property(e => e.Status)
            .IsRequired()
            .HasConversion<int>();

        builder.Property(e => e.ContextType)
            .IsRequired()
            .HasMaxLength(64);

        builder.Property(e => e.ContextPayload);

        builder.Property(e => e.EditableSectionIndicesJson);

        builder.Property(e => e.RequesterUserId)
            .IsRequired();

        builder.Property(e => e.InvitedAt)
            .IsRequired();

        builder.Property(e => e.UsedAt);

        builder.Property(e => e.TenantId)
            .IsRequired();

        builder.Property(e => e.CreatedAt).IsRequired();
        builder.Property(e => e.CreatedBy).IsRequired().HasMaxLength(256);
        builder.Property(e => e.UpdatedAt);
        builder.Property(e => e.UpdatedBy).HasMaxLength(256);
        builder.Property(e => e.IsDeleted).IsRequired().HasDefaultValue(false);

        builder.HasIndex(e => e.TokenHash)
            .IsUnique()
            .HasDatabaseName("ix_external_participant_invitations_token_hash");

        builder.HasIndex(e => new { e.WorkflowType, e.TargetEntityId, e.TargetEntitySubKey })
            .HasDatabaseName("ix_external_participant_invitations_target");

        builder.HasIndex(e => e.TenantId)
            .HasDatabaseName("ix_external_participant_invitations_tenant");
    }
}
