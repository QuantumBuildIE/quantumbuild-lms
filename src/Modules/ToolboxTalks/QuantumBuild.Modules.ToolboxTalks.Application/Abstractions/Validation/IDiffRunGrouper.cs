namespace QuantumBuild.Modules.ToolboxTalks.Application.Abstractions.Validation;

/// <summary>
/// Groups contiguous same-type diff operations into flaggable runs of length ≥ 2.
/// </summary>
public interface IDiffRunGrouper
{
    /// <summary>
    /// Groups the diff operations into qualifying runs.
    /// Only runs of length ≥ 2 are returned. Equal operations are never grouped.
    /// </summary>
    /// <param name="operations">Ordered word-level diff operations from IWordDiffService.</param>
    /// <returns>Qualifying runs with their original-text word indices and type.</returns>
    IReadOnlyList<DiffRun> Group(IReadOnlyList<DiffOperation> operations);
}

/// <summary>
/// A contiguous run of same-type diff operations, positioned by original-text word indices.
/// Word indices index into the original text's word stream (Equal + Delete advance the counter;
/// Insert does not).
/// </summary>
public record DiffRun(int StartWordIndex, int EndWordIndex, DiffType Type);
