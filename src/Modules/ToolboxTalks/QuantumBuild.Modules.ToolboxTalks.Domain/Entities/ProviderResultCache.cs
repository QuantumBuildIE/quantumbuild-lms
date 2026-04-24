using QuantumBuild.Core.Domain.Common;

namespace QuantumBuild.Modules.ToolboxTalks.Domain.Entities;

/// <summary>
/// System-level cache of provider back-translation results for corpus entries.
/// Shared across tenants — safe because corpus entries contain no PII
/// and are derived from already-accepted translations.
/// </summary>
public class ProviderResultCache : BaseEntity
{
    /// <summary>The corpus entry this result was computed for (SetNull on delete).</summary>
    public Guid? CorpusEntryId { get; set; }
    public AuditCorpusEntry? CorpusEntry { get; set; }

    /// <summary>Provider identifier: "haiku" | "deepl" | "gemini" | "sonnet".</summary>
    public string Provider { get; set; } = string.Empty;

    /// <summary>Model ID at the time the result was cached, e.g. "claude-haiku-4-5-20251001".</summary>
    public string ProviderVersion { get; set; } = string.Empty;

    public string BackTranslation { get; set; } = string.Empty;

    public int Score { get; set; }

    public DateTimeOffset ComputedAt { get; set; }
}
