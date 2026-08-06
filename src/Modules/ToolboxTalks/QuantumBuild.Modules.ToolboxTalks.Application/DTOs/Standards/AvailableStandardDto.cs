namespace QuantumBuild.Modules.ToolboxTalks.Application.DTOs.Standards;

/// <summary>
/// A Standard-kind RegulatoryBody the tenant may subscribe to, with subscription state.
/// </summary>
public record AvailableStandardDto
{
    public Guid Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public string Code { get; init; } = string.Empty;
    public string Country { get; init; } = string.Empty;
    public Guid SectorId { get; init; }
    public string SectorName { get; init; } = string.Empty;
    public bool IsSubscribed { get; init; }

    /// <summary>
    /// True when SectorId is not among the tenant's own active sectors (soft constraint —
    /// subscribing is still allowed, the UI surfaces this as an informational badge).
    /// </summary>
    public bool IsCrossSector { get; init; }
}
