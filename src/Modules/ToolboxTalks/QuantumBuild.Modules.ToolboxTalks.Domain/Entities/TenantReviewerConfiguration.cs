using QuantumBuild.Core.Domain.Common;

namespace QuantumBuild.Modules.ToolboxTalks.Domain.Entities;

/// <summary>
/// Per-tenant external reviewer configuration. One row per (TenantId, LanguageCode).
/// LanguageCode == null means "all languages" fallback — at most one such row per tenant.
/// </summary>
public class TenantReviewerConfiguration : TenantEntity
{
    /// <summary>
    /// Null means this is the tenant's "all languages" fallback reviewer.
    /// </summary>
    public string? LanguageCode { get; set; }

    public string ReviewerEmail { get; set; } = string.Empty;

    public string? ReviewerName { get; set; }
}
