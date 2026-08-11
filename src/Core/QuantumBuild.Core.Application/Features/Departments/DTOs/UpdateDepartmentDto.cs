namespace QuantumBuild.Core.Application.Features.Departments.DTOs;

public record UpdateDepartmentDto(
    string Name,
    string? Code,
    bool IsActive
);
