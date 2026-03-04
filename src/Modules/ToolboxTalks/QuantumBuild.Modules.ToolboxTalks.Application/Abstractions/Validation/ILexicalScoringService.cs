namespace QuantumBuild.Modules.ToolboxTalks.Application.Abstractions.Validation;

/// <summary>
/// Computes token-overlap similarity between two strings.
/// Returns a score from 0 (no overlap) to 100 (identical tokens).
/// </summary>
public interface ILexicalScoringService
{
    /// <summary>
    /// Calculates lexical similarity between two strings using token overlap.
    /// Algorithm: tokenise both (lowercase, strip punctuation), compute
    /// (matching tokens / max token count) * 100.
    /// </summary>
    /// <param name="original">The original (source) text</param>
    /// <param name="candidate">The candidate text to compare against</param>
    /// <returns>Similarity score 0-100</returns>
    double Score(string original, string candidate);
}
