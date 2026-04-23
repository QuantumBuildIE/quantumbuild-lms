using QuantumBuild.Core.Domain.Common;

namespace QuantumBuild.Modules.ToolboxTalks.Domain.Entities;

/// <summary>
/// System-level record of a translation pipeline configuration snapshot.
/// Shared across all tenants — no TenantId.
/// One row per distinct pipeline configuration; only one row has IsActive = true at any time.
/// </summary>
public class PipelineVersion : BaseEntity
{
    /// <summary>Human-readable version label, e.g. "6.4".</summary>
    public string Version { get; set; } = string.Empty;

    /// <summary>
    /// SHA-256 of ComponentsJson (first 12 hex chars, prefixed "sha256:").
    /// Two identical configurations always produce the same hash regardless of version string.
    /// </summary>
    public string Hash { get; set; } = string.Empty;

    /// <summary>JSON snapshot of all pipeline components at this version.</summary>
    public string ComponentsJson { get; set; } = string.Empty;

    /// <summary>UTC timestamp when this version record was computed.</summary>
    public DateTimeOffset ComputedAt { get; set; }

    /// <summary>Only one record can be active at a time; enforced in PipelineVersionService.</summary>
    public bool IsActive { get; set; }

    // Navigation
    public ICollection<TranslationValidationRun> Runs { get; set; } = new List<TranslationValidationRun>();
    public ICollection<PipelineChangeRecord> ChangeRecords { get; set; } = new List<PipelineChangeRecord>();
}
