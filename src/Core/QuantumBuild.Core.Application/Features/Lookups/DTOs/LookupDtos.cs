namespace QuantumBuild.Core.Application.Features.Lookups.DTOs;

public record LookupCategoryDto(
    Guid Id,
    string Name,
    string Module,
    bool AllowCustom,
    bool IsActive
);

public record LookupValueDto(
    Guid Id,
    Guid CategoryId,
    string Code,
    string Name,
    string? Metadata,
    int SortOrder,
    bool IsActive,
    bool IsGlobal,
    Guid? LookupValueId
);

public record CreateTenantLookupValueDto(
    string Code,
    string Name,
    string? Metadata,
    int SortOrder
);

public record UpdateTenantLookupValueDto(
    string Code,
    string Name,
    string? Metadata,
    int SortOrder,
    bool IsEnabled
);

public record ToggleGlobalValueDto(
    bool IsEnabled
);
