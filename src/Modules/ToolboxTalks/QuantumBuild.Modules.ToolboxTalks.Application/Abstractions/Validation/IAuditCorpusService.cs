using QuantumBuild.Core.Application.Models;
using QuantumBuild.Modules.ToolboxTalks.Application.DTOs.Validation;
using QuantumBuild.Modules.ToolboxTalks.Domain.Entities;
using QuantumBuild.Modules.ToolboxTalks.Domain.Enums;

namespace QuantumBuild.Modules.ToolboxTalks.Application.Abstractions.Validation;

/// <summary>
/// Manages audit corpora — frozen section pairs used for regression testing.
/// </summary>
public interface IAuditCorpusService
{
    Task<AuditCorpus> FreezeFromTalkAsync(
        Guid talkId,
        string name,
        string? description,
        IEnumerable<int> sectionIndexes,
        CancellationToken ct = default);

    Task<AuditCorpus> LockCorpusAsync(Guid corpusId, string signedBy, CancellationToken ct = default);

    Task<AuditCorpus> AddEntryAsync(Guid corpusId, AddCorpusEntryRequest request, CancellationToken ct = default);

    Task RemoveEntryAsync(Guid corpusId, Guid entryId, CancellationToken ct = default);

    /// <summary>
    /// Prepares a CorpusRun record and estimates cost.
    /// Does NOT enqueue the job — call ConfirmRunAsync to actually enqueue.
    /// </summary>
    Task<(CorpusRun Run, decimal EstimatedCostEur)> PrepareRunAsync(
        Guid corpusId,
        bool isSmokeTest,
        CorpusTriggerType triggerType,
        Guid? linkedPipelineChangeId,
        CancellationToken ct = default);

    /// <summary>
    /// Enqueues the job for an already-prepared CorpusRun.
    /// </summary>
    Task<CorpusRun> EnqueueRunAsync(Guid corpusRunId, CancellationToken ct = default);

    /// <summary>
    /// Convenience: prepare + enqueue in one step (used by auto-trigger path).
    /// Enforces 10-minute cooldown between runs for the same corpus.
    /// </summary>
    Task<CorpusRun> TriggerRunAsync(
        Guid corpusId,
        bool isSmokeTest,
        CorpusTriggerType triggerType,
        Guid? linkedPipelineChangeId,
        CancellationToken ct = default);

    Task<PaginatedList<AuditCorpusDto>> GetPagedAsync(int page, int pageSize, CancellationToken ct = default);

    Task<AuditCorpusDto?> GetByIdAsync(Guid corpusId, CancellationToken ct = default);

    Task<PaginatedList<CorpusRunSummaryDto>> GetRunsAsync(Guid corpusId, int page, int pageSize, CancellationToken ct = default);

    Task<CorpusRunDetailDto?> GetRunDetailAsync(Guid runId, CancellationToken ct = default);

    Task<CorpusRunDiffDto?> GetRunDiffAsync(Guid runId, CancellationToken ct = default);
}
