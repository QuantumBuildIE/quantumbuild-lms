namespace QuantumBuild.Modules.ToolboxTalks.Application.Abstractions.Validation;

/// <summary>
/// Splits text into sentence-level character spans.
/// </summary>
public interface ISentenceSplitter
{
    /// <summary>
    /// Splits the input text into sentence spans.
    /// Returns an empty list for empty or whitespace-only input.
    /// </summary>
    /// <param name="text">The text to split into sentences.</param>
    /// <returns>Ordered list of half-open character spans (Start inclusive, End exclusive).</returns>
    IReadOnlyList<SentenceSpan> Split(string text);
}

/// <summary>
/// A half-open character span representing a sentence within the source text.
/// Start is inclusive; End is exclusive.
/// </summary>
public record SentenceSpan(int Start, int End);
