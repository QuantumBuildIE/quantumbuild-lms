using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using QuantumBuild.Core.Domain.Entities;
using QuantumBuild.Core.Domain.Enums;

namespace QuantumBuild.Core.Infrastructure.Data.Configurations;

public class BulkImportSessionConfiguration : IEntityTypeConfiguration<BulkImportSession>
{
    public void Configure(EntityTypeBuilder<BulkImportSession> builder)
    {
        builder.HasKey(e => e.Id);

        builder.Property(e => e.CsvR2Key)
            .HasMaxLength(1000)
            .IsRequired();

        builder.Property(e => e.Status)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(20)
            .HasDefaultValue(BulkImportStatus.Uploaded);

        builder.Property(e => e.UploadedAt)
            .IsRequired();

        builder.Property(e => e.ProcessingStartedAt);

        builder.Property(e => e.ValidationResultJson)
            .HasColumnType("text");

        builder.Property(e => e.ProcessingResultJson)
            .HasColumnType("text");

        builder.Property(e => e.IsRerun)
            .IsRequired()
            .HasDefaultValue(false);

        builder.Property(e => e.TenantId)
            .IsRequired();

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

        builder.Property(e => e.DeletedBy)
            .HasMaxLength(256);

        // Composite index for admin list views and the future cleanup job.
        builder.HasIndex(e => new { e.TenantId, e.Status })
            .HasDatabaseName("IX_BulkImportSessions_TenantId_Status");

        // Index for the cleanup job: find sessions where UploadedAt < threshold.
        builder.HasIndex(e => e.UploadedAt)
            .HasDatabaseName("IX_BulkImportSessions_UploadedAt");
    }
}
