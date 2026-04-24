using QuantumBuild.Core.Domain.Common;
using QuantumBuild.Modules.ToolboxTalks.Domain.Enums;

namespace QuantumBuild.Modules.ToolboxTalks.Domain.Entities;

/// <summary>
/// A single reference section pair within an AuditCorpus.
/// Scoped to the parent corpus — no separate TenantId.
/// </summary>
public class AuditCorpusEntry : BaseEntity
{
    public Guid CorpusId { get; set; }
    public AuditCorpus Corpus { get; set; } = null!;

    /// <summary>Sequential ref within corpus, e.g. "CORPUS-001-E01".</summary>
    public string EntryRef { get; set; } = string.Empty;

    public string SectionTitle { get; set; } = string.Empty;

    public string OriginalText { get; set; } = string.Empty;

    /// <summary>Accepted translation at the time the corpus was frozen.</summary>
    public string TranslatedText { get; set; } = string.Empty;

    public string SourceLanguage { get; set; } = string.Empty;

    public string TargetLanguage { get; set; } = string.Empty;

    public string SectorKey { get; set; } = string.Empty;

    public int PassThreshold { get; set; }

    /// <summary>Expected validation outcome for this entry.</summary>
    public ValidationOutcome ExpectedOutcome { get; set; }

    public bool IsSafetyCritical { get; set; }

    /// <summary>Pipeline version snapshot at the time this entry was frozen.</summary>
    public Guid? PipelineVersionIdAtFreeze { get; set; }

    /// <summary>JSON array of topic tags for filtering and reporting.</summary>
    public string? TagsJson { get; set; }

    /// <summary>False = soft-removed from runs but retained for audit trail.</summary>
    public bool IsActive { get; set; } = true;
}
