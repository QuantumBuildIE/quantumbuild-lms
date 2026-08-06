using QuantumBuild.Core.Domain.Common;
using QuantumBuild.Modules.ToolboxTalks.Domain.Enums;

namespace QuantumBuild.Modules.ToolboxTalks.Domain.Entities;

/// <summary>
/// Records a tenant's voluntary subscription to a Standard-kind RegulatoryBody.
/// Composite unique index on {TenantId, RegulatoryBodyId} — a tenant subscribes to a given
/// standard at most once.
/// </summary>
public class TenantStandardSubscription : TenantEntity
{
    public Guid RegulatoryBodyId { get; set; }

    // Navigation properties
    public RegulatoryBody RegulatoryBody { get; set; } = null!;

    /// <summary>
    /// Creates a subscription, enforcing that the target body is Kind = Standard.
    /// Regulation bodies apply automatically via sector and cannot be subscribed to.
    /// </summary>
    public static TenantStandardSubscription Create(Guid tenantId, RegulatoryBody regulatoryBody)
    {
        if (regulatoryBody.Kind != RegulatoryBodyKind.Standard)
            throw new InvalidOperationException("Tenants can only subscribe to regulatory bodies with Kind = Standard.");

        return new TenantStandardSubscription
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            RegulatoryBodyId = regulatoryBody.Id,
            RegulatoryBody = regulatoryBody
        };
    }
}
