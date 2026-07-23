namespace QuantumBuild.Modules.ToolboxTalks.Application.DTOs.Standards;

/// <summary>
/// A tenant's active subscription to a Standard-kind RegulatoryBody.
/// </summary>
public record TenantStandardSubscriptionDto
{
    public Guid Id { get; init; }
    public Guid TenantId { get; init; }
    public Guid RegulatoryBodyId { get; init; }
    public string Name { get; init; } = string.Empty;
    public string Code { get; init; } = string.Empty;
    public string Country { get; init; } = string.Empty;
    public Guid SectorId { get; init; }
    public string SectorName { get; init; } = string.Empty;
    public bool IsCrossSector { get; init; }
    public DateTime SubscribedAt { get; init; }
}
