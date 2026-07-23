namespace QuantumBuild.Modules.ToolboxTalks.Application.DTOs.Frameworks;

/// <summary>
/// A regulatory framework (Regulation or Standard) that currently applies to a tenant,
/// either because the tenant is assigned the framework's sector (Regulation) or because
/// the tenant holds an active subscription to it (Standard).
/// </summary>
public record ApplicableFrameworkDto(
    Guid RegulatoryBodyId,
    string BodyName,
    string BodyCode,
    string Kind,
    string SectorKey,
    string SectorName,
    string Source,
    int ApprovedRequirementCount
);

/// <summary>
/// Raw entitlement sets used to gate requirement visibility: the tenant's assigned
/// sector keys (Regulation entitlement) and the RegulatoryBodyIds of Standards the
/// tenant currently subscribes to (Standard entitlement, independent of sector).
/// </summary>
public record TenantEntitlementsDto(
    List<string> SectorKeys,
    List<Guid> SubscribedStandardBodyIds
);
