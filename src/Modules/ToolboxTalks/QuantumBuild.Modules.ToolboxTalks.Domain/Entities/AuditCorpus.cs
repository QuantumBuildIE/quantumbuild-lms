using QuantumBuild.Core.Domain.Common;

namespace QuantumBuild.Modules.ToolboxTalks.Domain.Entities;

/// <summary>
/// A frozen set of reference section pairs (source + accepted translation) used to
/// regression-test the validation pipeline when changes are made.
/// </summary>
public class AuditCorpus : TenantEntity
{
    /// <summary>Sequential per-tenant identifier, e.g. "CORPUS-001".</summary>
    public string CorpusId { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    public string SectorKey { get; set; } = string.Empty;

    /// <summary>Language pair in BCP-47 format, e.g. "en-pl".</summary>
    public string LanguagePair { get; set; } = string.Empty;

    /// <summary>Source talk this corpus was frozen from (nullable — corpus survives talk deletion).</summary>
    public Guid? SourceTalkId { get; set; }
    public ToolboxTalk? SourceTalk { get; set; }

    /// <summary>Pipeline version that was active when the corpus was frozen.</summary>
    public Guid? FrozenFromPipelineVersionId { get; set; }
    public PipelineVersion? FrozenFromPipelineVersion { get; set; }

    /// <summary>When locked, no entry changes are allowed.</summary>
    public bool IsLocked { get; set; }

    public DateTimeOffset? LockedAt { get; set; }

    /// <summary>Name of the person who locked the corpus.</summary>
    public string? LockedBy { get; set; }

    /// <summary>Name of the person who signed off the corpus.</summary>
    public string? SignedBy { get; set; }

    /// <summary>Incremented each time entries change — tracks corpus evolution.</summary>
    public int Version { get; set; } = 1;

    public ICollection<AuditCorpusEntry> Entries { get; set; } = new List<AuditCorpusEntry>();
    public ICollection<CorpusRun> Runs { get; set; } = new List<CorpusRun>();
}
