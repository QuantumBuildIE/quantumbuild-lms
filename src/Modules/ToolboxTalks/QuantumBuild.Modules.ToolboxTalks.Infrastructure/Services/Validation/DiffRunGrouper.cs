using QuantumBuild.Modules.ToolboxTalks.Application.Abstractions.Validation;

namespace QuantumBuild.Modules.ToolboxTalks.Infrastructure.Services.Validation;

/// <summary>
/// Groups contiguous same-type diff operations into flaggable runs of length ≥ 2.
/// Maintains an original-text word counter: advances on Equal and Delete, not on Insert.
/// </summary>
public class DiffRunGrouper : IDiffRunGrouper
{
    /// <inheritdoc />
    public IReadOnlyList<DiffRun> Group(IReadOnlyList<DiffOperation> operations)
    {
        if (operations.Count == 0)
            return [];

        var result = new List<DiffRun>();

        // Original-text word position counter (Equal and Delete advance it; Insert does not)
        int counter = 0;

        // Current-run state
        int runLength = 0;
        DiffType runType = default;
        int runStartWordIdx = 0;
        int runEndWordIdx = 0;

        void FlushRun()
        {
            if (runLength >= 2)
                result.Add(new DiffRun(runStartWordIdx, runEndWordIdx, runType));
            runLength = 0;
        }

        foreach (var op in operations)
        {
            switch (op.Type)
            {
                case DiffType.Equal:
                    FlushRun();
                    counter++;
                    break;

                case DiffType.Delete:
                    if (runLength > 0 && runType == DiffType.Delete)
                    {
                        runEndWordIdx = counter;
                        counter++;
                        runLength++;
                    }
                    else
                    {
                        FlushRun();
                        runType = DiffType.Delete;
                        runStartWordIdx = counter;
                        runEndWordIdx = counter;
                        counter++;
                        runLength = 1;
                    }
                    break;

                case DiffType.Insert:
                    if (runLength > 0 && runType == DiffType.Insert)
                    {
                        // Insert does not advance the counter
                        runLength++;
                    }
                    else
                    {
                        FlushRun();
                        runType = DiffType.Insert;
                        // Both indices point to the original-word position preceding the insertion
                        runStartWordIdx = counter;
                        runEndWordIdx = counter;
                        runLength = 1;
                    }
                    break;
            }
        }

        FlushRun();
        return result;
    }
}
