using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using QuantumBuild.Core.Domain.Entities;

namespace QuantumBuild.Core.Infrastructure.Data.Configurations;

public class SupervisorAssignmentConfiguration : IEntityTypeConfiguration<SupervisorAssignment>
{
    public void Configure(EntityTypeBuilder<SupervisorAssignment> builder)
    {
        builder.HasKey(e => e.Id);

        builder.HasIndex(e => new { e.TenantId, e.SupervisorEmployeeId, e.OperatorEmployeeId })
            .IsUnique()
            .HasDatabaseName("IX_SupervisorAssignments_TenantId_SupervisorEmployeeId_OperatorEmployeeId");

        builder.HasOne(e => e.Supervisor)
            .WithMany(e => e.SupervisorAssignments)
            .HasForeignKey(e => e.SupervisorEmployeeId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(e => e.Operator)
            .WithMany(e => e.OperatorAssignments)
            .HasForeignKey(e => e.OperatorEmployeeId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
