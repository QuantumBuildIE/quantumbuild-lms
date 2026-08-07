using QuantumBuild.Core.Domain.Common;

namespace QuantumBuild.Modules.ToolboxTalks.Domain.Entities;

/// <summary>
/// Per-tenant, per-prefix, per-year running counter used to allocate certificate numbers
/// atomically. Rows are never soft-deleted or read via LINQ - allocation happens through a
/// single INSERT ... ON CONFLICT DO UPDATE ... RETURNING statement, which is what makes the
/// allocation safe under concurrent callers (see CertificateGenerationService).
/// </summary>
public class CertificateNumberCounter : TenantEntity
{
    /// <summary>
    /// Certificate number prefix this counter tracks (e.g. "LRN", "TBC") - tenant-configurable.
    /// </summary>
    public string Prefix { get; set; } = string.Empty;

    /// <summary>
    /// Calendar year (UTC) this counter tracks. A new row is created each year.
    /// </summary>
    public int Year { get; set; }

    /// <summary>
    /// The last sequence number allocated for this {TenantId, Prefix, Year} triple.
    /// </summary>
    public int LastNumber { get; set; }
}
