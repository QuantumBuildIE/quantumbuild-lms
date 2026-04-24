using QuantumBuild.Core.Domain.Common;
using QuantumBuild.Modules.ToolboxTalks.Domain.Enums;

namespace QuantumBuild.Modules.ToolboxTalks.Domain.Entities;

/// <summary>
/// A single execution of the validation pipeline against an AuditCorpus.
/// Used to detect regressions when pipeline components change.
/// </summary>
public class CorpusRun : TenantEntity
{
    public Guid CorpusId { get; set; }
    public AuditCorpus AuditCorpus { get; set; } = null!;

    /// <summary>Pipeline version that was being tested in this run.</summary>
    public Guid? PipelineVersionId { get; set; }
    public PipelineVersion? PipelineVersion { get; set; }

    /// <summary>The pipeline change record that triggered this run, if auto-triggered.</summary>
    public Guid? LinkedPipelineChangeId { get; set; }
    public PipelineChangeRecord? LinkedPipelineChange { get; set; }

    public CorpusTriggerType TriggerType { get; set; }

    /// <summary>Name of the person or system that triggered the run.</summary>
    public string? TriggeredBy { get; set; }

    /// <summary>Smoke test = process first 5 entries only.</summary>
    public bool IsSmokeTest { get; set; }

    public CorpusRunStatus Status { get; set; } = CorpusRunStatus.Pending;

    public DateTimeOffset? StartedAt { get; set; }

    public DateTimeOffset? CompletedAt { get; set; }

    public int TotalEntries { get; set; }

    public int PassedEntries { get; set; }

    public int ReviewEntries { get; set; }

    public int FailedEntries { get; set; }

    /// <summary>Number of entries that scored worse than their ExpectedOutcome.</summary>
    public int RegressionEntries { get; set; }

    public decimal? MeanScore { get; set; }

    /// <summary>Worst score drop in points across all regressed entries.</summary>
    public int? MaxScoreDrop { get; set; }

    public CorpusVerdict? Verdict { get; set; }

    /// <summary>% of entries that must fail to trigger a block verdict. Default 20.</summary>
    public int FailureThresholdPercent { get; set; } = 20;

    /// <summary>Score drop in points that counts as a regression. Default 10.</summary>
    public int ScoreDropThreshold { get; set; } = 10;

    public decimal? EstimatedCostEur { get; set; }

    public decimal? ActualCostEur { get; set; }

    public string? ErrorMessage { get; set; }

    public ICollection<CorpusRunResult> Results { get; set; } = new List<CorpusRunResult>();
}
