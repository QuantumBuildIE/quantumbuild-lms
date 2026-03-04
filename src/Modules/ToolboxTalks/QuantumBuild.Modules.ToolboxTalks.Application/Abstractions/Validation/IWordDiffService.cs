namespace QuantumBuild.Modules.ToolboxTalks.Application.Abstractions.Validation;

/// <summary>
/// Produces a word-level diff between two strings using the
/// Longest Common Subsequence (LCS) algorithm.
/// </summary>
public interface IWordDiffService
{
    /// <summary>
    /// Computes a word-level diff between the original and candidate text.
    /// </summary>
    /// <param name="original">The original (source) text</param>
    /// <param name="candidate">The candidate text to compare against</param>
    /// <returns>Diff result with operations and summary statistics</returns>
    WordDiffResult Diff(string original, string candidate);
}

/// <summary>
/// Result of a word-level diff operation.
/// </summary>
public class WordDiffResult
{
    /// <summary>
    /// Ordered list of diff operations (Equal, Insert, Delete).
    /// </summary>
    public List<DiffOperation> Operations { get; set; } = [];

    /// <summary>
    /// Number of words present in both texts (Equal operations).
    /// </summary>
    public int MatchingWordCount { get; set; }

    /// <summary>
    /// Number of words added in the candidate (Insert operations).
    /// </summary>
    public int InsertedCount { get; set; }

    /// <summary>
    /// Number of words removed from the original (Delete operations).
    /// </summary>
    public int DeletedCount { get; set; }

    /// <summary>
    /// Similarity percentage: matching / max(original words, candidate words) * 100.
    /// </summary>
    public double SimilarityPercentage { get; set; }
}

/// <summary>
/// A single word-level diff operation.
/// </summary>
public class DiffOperation
{
    /// <summary>
    /// The type of operation.
    /// </summary>
    public DiffType Type { get; set; }

    /// <summary>
    /// The word associated with this operation.
    /// </summary>
    public string Word { get; set; } = string.Empty;
}

/// <summary>
/// Type of diff operation in a word-level diff.
/// </summary>
public enum DiffType
{
    /// <summary>Word is present in both original and candidate.</summary>
    Equal,

    /// <summary>Word was inserted (present in candidate only).</summary>
    Insert,

    /// <summary>Word was deleted (present in original only).</summary>
    Delete
}
