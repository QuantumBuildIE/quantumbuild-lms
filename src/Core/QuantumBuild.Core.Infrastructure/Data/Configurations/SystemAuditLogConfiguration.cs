using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using QuantumBuild.Core.Domain.Entities;

namespace QuantumBuild.Core.Infrastructure.Data.Configurations;

public class SystemAuditLogConfiguration : IEntityTypeConfiguration<SystemAuditLog>
{
    public void Configure(EntityTypeBuilder<SystemAuditLog> builder)
    {
        builder.ToTable("SystemAuditLogs");

        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).ValueGeneratedOnAdd();

        builder.Property(e => e.Action)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(e => e.UserName)
            .HasMaxLength(256);

        builder.Property(e => e.EntityType)
            .HasMaxLength(100);

        builder.Property(e => e.EntityDisplayName)
            .HasMaxLength(300);

        builder.Property(e => e.IpAddress)
            .HasMaxLength(50);

        builder.Property(e => e.FailureReason)
            .HasMaxLength(500);

        builder.HasIndex(e => e.TenantId)
            .HasDatabaseName("IX_SystemAuditLogs_TenantId");

        builder.HasIndex(e => e.OccurredAt)
            .HasDatabaseName("IX_SystemAuditLogs_OccurredAt");

        builder.HasIndex(e => e.Action)
            .HasDatabaseName("IX_SystemAuditLogs_Action");
    }
}
