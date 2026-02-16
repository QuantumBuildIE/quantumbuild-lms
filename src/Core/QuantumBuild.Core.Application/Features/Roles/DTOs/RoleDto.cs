namespace QuantumBuild.Core.Application.Features.Roles.DTOs;

public record RoleDto(
    Guid Id,
    string Name,
    string? Description,
    int PermissionCount
);
