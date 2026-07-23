using QuantumBuild.Modules.ToolboxTalks.Application.DTOs.Frameworks;

namespace QuantumBuild.Modules.ToolboxTalks.Application.Abstractions.Frameworks;

/// <summary>
/// Resolves which regulatory frameworks currently apply to a tenant — the union of
/// Regulations applied via the tenant's assigned sectors and Standards applied via
/// active TenantStandardSubscription rows. Shared by the compliance checklist, the
/// regulatory browse page, and requirement-mapping provenance/attribution.
/// </summary>
public interface IApplicableFrameworksService
{
    /// <summary>
    /// Raw entitlement sets — the tenant's sector keys and subscribed Standard body IDs.
    /// Use this when you only need to gate a query, not render a display list.
    /// </summary>
    Task<TenantEntitlementsDto> GetTenantEntitlementsAsync(
        Guid tenantId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Full per-body-per-sector list of frameworks applicable to the tenant, with approved
    /// requirement counts, for display (e.g. the compliance page's top summary).
    /// </summary>
    Task<List<ApplicableFrameworkDto>> GetApplicableFrameworksAsync(
        Guid tenantId, CancellationToken cancellationToken = default);
}
