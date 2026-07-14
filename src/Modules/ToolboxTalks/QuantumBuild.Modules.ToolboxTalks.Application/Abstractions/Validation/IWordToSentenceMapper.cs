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
    /// Deduplicated list of <see cref="SentenceFlagSpan"/> values in sentence-first-hit order.
    /// Each span carries the DiffRuns that anchored to it.
    /// Returns an empty list when the original text has no words.
    /// </returns>
    IReadOnlyList<SentenceFlagSpan> Map(
        string originalText,
        IReadOnlyList<SentenceSpan> sentences,
        IReadOnlyList<DiffRun> diffRuns);
}

/// <summary>
/// A sentence character span together with the diff runs that map to it.
/// StartOffset is inclusive; EndOffset is exclusive.
/// </summary>
public record SentenceFlagSpan(
    int StartOffset,
    int EndOffset,
    IReadOnlyList<DiffRun> Runs);
