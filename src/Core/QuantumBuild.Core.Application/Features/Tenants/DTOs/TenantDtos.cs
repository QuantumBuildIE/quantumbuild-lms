using QuantumBuild.Core.Domain.Enums;

namespace QuantumBuild.Core.Application.Features.Tenants.DTOs;

public record TenantListDto(
    Guid Id,
    string Name,
    string? Code,
    string? CompanyName,
    TenantStatus Status,
    string? ContactEmail,
    string? ContactName,
    bool IsActive,
    DateTime CreatedAt
);

public record TenantDetailDto(
    Guid Id,
    string Name,
    string? Code,
    string? CompanyName,
    TenantStatus Status,
    string? ContactEmail,
    string? ContactName,
    bool IsActive,
    DateTime CreatedAt,
    string CreatedBy,
    DateTime? UpdatedAt,
    string? UpdatedBy
);

public record CreateTenantCommand(
    string Name,
    string? Code,
    string? CompanyName,
    string? ContactEmail,
    string? ContactName
);

public record UpdateTenantCommand(
    string Name,
    string? Code,
    string? CompanyName,
    string? ContactEmail,
    string? ContactName
);

public record UpdateTenantStatusCommand(
    TenantStatus Status
);
