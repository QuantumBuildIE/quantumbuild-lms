using QuantumBuild.Core.Domain.Common;
using QuantumBuild.Modules.ToolboxTalks.Domain.Enums;

namespace QuantumBuild.Modules.ToolboxTalks.Domain.Entities;

/// <summary>
/// System-managed regulatory document entity (no TenantId).
/// e.g. "Draft National Standards for Home Support Services".
/// </summary>
public class RegulatoryDocument : BaseEntity
{
    public Guid RegulatoryBodyId { get; set; }

    public string Title { get; set; } = string.Empty;

    public string Version { get; set; } = string.Empty;

    public DateOnly? EffectiveDate { get; set; }

    public string? Source { get; set; }

    public string? SourceUrl { get; set; }

    public bool IsActive { get; set; } = true;

    /// <summary>
    /// Timestamp of the last successful AI ingestion of requirements from this document.
    /// </summary>
    public DateTimeOffset? LastIngestedAt { get; set; }

    /// <summary>
    /// State of the most recent ingestion attempt (Idle/Ingesting/Success/Failed).
    /// Set by RequirementIngestionJob at every stage — including failure paths — so
    /// the frontend can distinguish "never run" from "ran and failed".
    /// </summary>
    public RegulatoryIngestionStatus LastIngestionStatus { get; set; } = RegulatoryIngestionStatus.Idle;

    /// <summary>
    /// Human-readable failure reason for the most recent ingestion attempt.
    /// Null unless LastIngestionStatus is Failed.
    /// </summary>
    public string? LastIngestionErrorMessage { get; set; }

    /// <summary>
    /// Failure category for the most recent ingestion attempt: "invalid_uri",
    /// "fetch_failed", "parse_failed", or "unknown". Null unless LastIngestionStatus is Failed.
    /// </summary>
    public string? LastIngestionErrorCode { get; set; }

    // Navigation properties
    public RegulatoryBody RegulatoryBody { get; set; } = null!;
    public ICollection<RegulatoryProfile> Profiles { get; set; } = new List<RegulatoryProfile>();
}
