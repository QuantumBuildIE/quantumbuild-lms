using QuantumBuild.Core.Domain.Common;

namespace QuantumBuild.Core.Domain.Entities;

public class SupervisorAssignment : TenantEntity
{
    public Guid SupervisorEmployeeId { get; set; }

    public Guid OperatorEmployeeId { get; set; }

    public Employee Supervisor { get; set; } = null!;

    public Employee Operator { get; set; } = null!;
}
