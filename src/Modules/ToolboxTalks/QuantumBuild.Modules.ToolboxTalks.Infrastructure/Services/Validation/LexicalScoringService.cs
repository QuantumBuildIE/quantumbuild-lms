using System.Text.RegularExpressions;
using QuantumBuild.Modules.ToolboxTalks.Application.Abstractions.Validation;

namespace QuantumBuild.Modules.ToolboxTalks.Infrastructure.Services.Validation;

/// <summary>
/// Computes token-overlap similarity between two strings.
/// Tokenises both strings (lowercase, strip punctuation), then calculates
/// (matching tokens / max token count) * 100.
/// </summary>
public partial class LexicalScoringService : ILexicalScoringService
{
    /// <inheritdoc />
    public double Score(string original, string candidate)
    {
        if (string.IsNullOrWhiteSpace(original) && string.IsNullOrWhiteSpace(candidate))
            return 100.0;

        if (string.IsNullOrWhiteSpace(original) || string.IsNullOrWhiteSpace(candidate))
            return 0.0;

        var originalTokens = Tokenise(original);
        var candidateTokens = Tokenise(candidate);

        if (originalTokens.Count == 0 && candidateTokens.Count == 0)
            return 100.0;

        if (originalTokens.Count == 0 || candidateTokens.Count == 0)
            return 0.0;

        // Build a bag from candidate tokens so we can match each occurrence once
        var candidateBag = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var token in candidateTokens)
        {
            candidateBag.TryGetValue(token, out var count);
            candidateBag[token] = count + 1;
        }

        var matchCount = 0;
        foreach (var token in originalTokens)
        {
            if (candidateBag.TryGetValue(token, out var count) && count > 0)
            {
                matchCount++;
                candidateBag[token] = count - 1;
            }
        }

        var maxCount = Math.Max(originalTokens.Count, candidateTokens.Count);
        return (double)matchCount / maxCount * 100.0;
    }

    /// <summary>
    /// Splits text into lowercase tokens, stripping punctuation.
    /// </summary>
    private static List<string> Tokenise(string text)
    {
        // Strip punctuation, then split on whitespace
        var cleaned = PunctuationRegex().Replace(text.ToLowerInvariant(), " ");
        var tokens = cleaned.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return [.. tokens];
    }

    [GeneratedRegex(@"[^\w\s]", RegexOptions.Compiled)]
    private static partial Regex PunctuationRegex();
}
