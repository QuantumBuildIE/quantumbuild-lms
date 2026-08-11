namespace QuantumBuild.Core.Application.Features.Departments.DTOs;

public record CreateDepartmentDto(
    string Name,
    string? Code,
    bool IsActive
);
