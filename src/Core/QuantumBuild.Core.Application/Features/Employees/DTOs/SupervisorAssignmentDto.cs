namespace QuantumBuild.Core.Application.Features.Employees.DTOs;

public record SupervisorAssignmentDto(
    Guid Id,
    Guid SupervisorEmployeeId,
    string SupervisorName,
    Guid OperatorEmployeeId,
    string OperatorName,
    DateTime AssignedAt,
    string AssignedBy
);
