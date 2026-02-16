using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using QuantumBuild.Modules.ToolboxTalks.Domain.Entities;

namespace QuantumBuild.Modules.ToolboxTalks.Infrastructure.Persistence.Configurations;

/// <summary>
/// Entity Framework configuration for ToolboxTalkSettings entity
/// </summary>
public class ToolboxTalkSettingsConfiguration : IEntityTypeConfiguration<ToolboxTalkSettings>
{
    public void Configure(EntityTypeBuilder<ToolboxTalkSettings> builder)
    {
        // Table name
        builder.ToTable("ToolboxTalkSettings", "toolbox_talks");

        // Primary key
        builder.HasKey(s => s.Id);

        // Properties
        builder.Property(s => s.TenantId)
            .IsRequired();

        builder.Property(s => s.DefaultDueDays)
            .IsRequired()
            .HasDefaultValue(7);

        builder.Property(s => s.ReminderFrequencyDays)
            .IsRequired()
            .HasDefaultValue(1);

        builder.Property(s => s.MaxReminders)
            .IsRequired()
            .HasDefaultValue(5);

        builder.Property(s => s.EscalateAfterReminders)
            .IsRequired()
            .HasDefaultValue(3);

        builder.Property(s => s.RequireVideoCompletion)
            .IsRequired()
            .HasDefaultValue(true);

        builder.Property(s => s.DefaultPassingScore)
            .IsRequired()
            .HasDefaultValue(80);

        builder.Property(s => s.EnableTranslation)
            .IsRequired()
            .HasDefaultValue(false);

        builder.Property(s => s.TranslationProvider)
            .HasMaxLength(50);

        builder.Property(s => s.EnableVideoDubbing)
            .IsRequired()
            .HasDefaultValue(false);

        builder.Property(s => s.VideoDubbingProvider)
            .HasMaxLength(50);

        builder.Property(s => s.NotificationEmailTemplate);

        builder.Property(s => s.ReminderEmailTemplate);

        // Audit fields
        builder.Property(s => s.CreatedAt)
            .IsRequired();

        builder.Property(s => s.CreatedBy)
            .IsRequired()
            .HasMaxLength(256);

        builder.Property(s => s.UpdatedAt);

        builder.Property(s => s.UpdatedBy)
            .HasMaxLength(256);

        builder.Property(s => s.IsDeleted)
            .IsRequired()
            .HasDefaultValue(false);

        // Indexes - Unique on TenantId (one settings record per tenant)
        builder.HasIndex(s => s.TenantId)
            .IsUnique()
            .HasDatabaseName("ix_toolbox_talk_settings_tenant");

        // Query filter for soft delete
        builder.HasQueryFilter(s => !s.IsDeleted);
    }
}
