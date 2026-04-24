using QuantumBuild.Modules.ToolboxTalks.Domain.Entities;

namespace QuantumBuild.Modules.ToolboxTalks.Application.Abstractions.Validation;

/// <summary>
/// Estimates the EUR cost of running a corpus against the validation pipeline.
/// </summary>
public interface ICostEstimationService
{
    /// <summary>
    /// Estimates the total EUR cost for a corpus run.
    /// </summary>
    /// <param name="entries">Corpus entries to estimate for</param>
    /// <param name="maxRounds">Maximum pipeline rounds allowed</param>
    /// <param name="isSmokeTest">If true, only 5 entries are processed</param>
    decimal EstimateCorpusRunCostEur(
        IEnumerable<AuditCorpusEntry> entries,
        int maxRounds,
        bool isSmokeTest);
}
