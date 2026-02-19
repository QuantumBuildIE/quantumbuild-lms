namespace QuantumBuild.Core.Application.Features.Employees.DTOs;

public record SupervisorOperatorDto(
    Guid EmployeeId,
    string EmployeeCode,
    string FullName,
    string? Department,
    string? JobTitle
);
