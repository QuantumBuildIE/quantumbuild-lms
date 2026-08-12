namespace QuantumBuild.Core.Application.Features.Departments.DTOs;

public record DepartmentDto(
    Guid Id,
    string Name,
    string? Code,
    bool IsActive
);
