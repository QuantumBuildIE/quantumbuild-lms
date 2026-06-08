namespace QuantumBuild.Modules.ToolboxTalks.Application.Abstractions.Validation;

/// <summary>
/// Maps word-indexed diff runs to sentence-level character spans in the original text.
/// </summary>
public interface IWordToSentenceMapper
{
    /// <summary>
    /// Maps each diff run to the sentence span in the original text that contains it.
    /// Multiple runs falling in the same sentence produce a single deduplicated entry.
    /// </summary>
    /// <param name="originalText">The original (source) text.</param>
    /// <param name="sentences">Sentence spans produced by ISentenceSplitter.</param>
    /// <param name="diffRuns">Qualifying diff runs produced by IDiffRunGrouper.</param>
    /// <returns>
    /// Deduplicated list of (StartOffset, EndOffset) sentence character spans.
    /// Returns an empty list when the original text has no words.
    /// </returns>
    IReadOnlyList<(int StartOffset, int EndOffset)> Map(
        string originalText,
        IReadOnlyList<SentenceSpan> sentences,
        IReadOnlyList<DiffRun> diffRuns);
}
