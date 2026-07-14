using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using QuantumBuild.Modules.ToolboxTalks.Domain.Entities;
using QuantumBuild.Modules.ToolboxTalks.Domain.Enums;

namespace QuantumBuild.Modules.ToolboxTalks.Infrastructure.Persistence.Configurations;

public class TranslationFlagConfiguration : IEntityTypeConfiguration<TranslationFlag>
{
    public void Configure(EntityTypeBuilder<TranslationFlag> builder)
    {
        builder.ToTable("TranslationFlags", "toolbox_talks");
        builder.HasKey(e => e.Id);

        builder.Property(e => e.ToolboxTalkId)
            .IsRequired();

        builder.Property(e => e.LanguageCode)
            .IsRequired()
            .HasMaxLength(8);

        builder.Property(e => e.StartOffset)
            .IsRequired();

        builder.Property(e => e.EndOffset)
            .IsRequired();

        builder.Property(e => e.Severity)
            .IsRequired()
            .HasConversion<int>();

        builder.Property(e => e.Reason)
            .IsRequired()
            .HasMaxLength(512);

        builder.Property(e => e.TenantId)
            .IsRequired();

        builder.Property(e => e.CreatedAt).IsRequired();
        builder.Property(e => e.CreatedBy).IsRequired().HasMaxLength(256);
        builder.Property(e => e.UpdatedAt);
        builder.Property(e => e.UpdatedBy).HasMaxLength(256);
        builder.Property(e => e.IsDeleted).IsRequired().HasDefaultValue(false);

        builder.Property(e => e.ValidationResultId)
            .IsRequired();

        builder.HasOne(e => e.ToolboxTalk)
            .WithMany()
            .HasForeignKey(e => e.ToolboxTalkId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(e => e.ValidationResult)
            .WithMany(r => r.Flags)
            .HasForeignKey(e => e.ValidationResultId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(e => new { e.ToolboxTalkId, e.LanguageCode })
            .HasDatabaseName("ix_translation_flags_talk_language");

        builder.HasIndex(e => e.ValidationResultId)
            .HasDatabaseName("ix_translation_flags_validation_result");

        builder.HasIndex(e => e.TenantId)
            .HasDatabaseName("ix_translation_flags_tenant");
    }
}
