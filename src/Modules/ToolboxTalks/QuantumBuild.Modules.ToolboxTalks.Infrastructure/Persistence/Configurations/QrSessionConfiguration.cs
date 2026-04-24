using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using QuantumBuild.Modules.ToolboxTalks.Domain.Entities;

namespace QuantumBuild.Modules.ToolboxTalks.Infrastructure.Persistence.Configurations;

public class QrSessionConfiguration : IEntityTypeConfiguration<QrSession>
{
    public void Configure(EntityTypeBuilder<QrSession> builder)
    {
        builder.ToTable("QrSessions", "toolbox_talks");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.EmployeeId).IsRequired();
        builder.Property(x => x.QrCodeId).IsRequired();
        builder.Property(x => x.SessionToken).IsRequired();
        builder.Property(x => x.Language).IsRequired().HasMaxLength(10);
        builder.Property(x => x.ContentMode).IsRequired().HasConversion<string>().HasMaxLength(50);
        builder.Property(x => x.Status).IsRequired().HasConversion<string>().HasMaxLength(50).HasDefaultValue("Active");
        builder.Property(x => x.StartedAt).IsRequired();

        builder.Property(x => x.CreatedAt).IsRequired();
        builder.Property(x => x.CreatedBy).IsRequired().HasMaxLength(256);
        builder.Property(x => x.UpdatedAt);
        builder.Property(x => x.UpdatedBy).HasMaxLength(256);
        builder.Property(x => x.IsDeleted).IsRequired().HasDefaultValue(false);
        builder.Property(x => x.DeletedBy);

        builder.HasOne(x => x.Employee)
            .WithMany()
            .HasForeignKey(x => x.EmployeeId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.QrCode)
            .WithMany()
            .HasForeignKey(x => x.QrCodeId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(x => x.SessionToken).IsUnique().HasDatabaseName("ix_qr_sessions_token");
        builder.HasIndex(x => x.TenantId).HasDatabaseName("ix_qr_sessions_tenant");
    }
}
