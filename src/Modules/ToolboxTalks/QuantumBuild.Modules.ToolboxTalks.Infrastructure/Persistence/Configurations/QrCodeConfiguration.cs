using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using QuantumBuild.Modules.ToolboxTalks.Domain.Entities;

namespace QuantumBuild.Modules.ToolboxTalks.Infrastructure.Persistence.Configurations;

public class QrCodeConfiguration : IEntityTypeConfiguration<QrCode>
{
    public void Configure(EntityTypeBuilder<QrCode> builder)
    {
        builder.ToTable("QrCodes", "toolbox_talks");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.QrLocationId).IsRequired();
        builder.Property(x => x.Name).IsRequired().HasMaxLength(200);
        builder.Property(x => x.ContentMode).IsRequired().HasConversion<string>().HasMaxLength(50);
        builder.Property(x => x.CodeToken).IsRequired().HasMaxLength(36);
        builder.Property(x => x.IsActive).IsRequired().HasDefaultValue(true);
        builder.Property(x => x.QrImageUrl).HasMaxLength(2048);

        builder.Property(x => x.CreatedAt).IsRequired();
        builder.Property(x => x.CreatedBy).IsRequired().HasMaxLength(256);
        builder.Property(x => x.UpdatedAt);
        builder.Property(x => x.UpdatedBy).HasMaxLength(256);
        builder.Property(x => x.IsDeleted).IsRequired().HasDefaultValue(false);
        builder.Property(x => x.DeletedBy);

        builder.HasOne(x => x.QrLocation)
            .WithMany(x => x.QrCodes)
            .HasForeignKey(x => x.QrLocationId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.ToolboxTalk)
            .WithMany()
            .HasForeignKey(x => x.ToolboxTalkId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasIndex(x => x.CodeToken).IsUnique().HasDatabaseName("ix_qr_codes_token");
        builder.HasIndex(x => x.TenantId).HasDatabaseName("ix_qr_codes_tenant");
    }
}
