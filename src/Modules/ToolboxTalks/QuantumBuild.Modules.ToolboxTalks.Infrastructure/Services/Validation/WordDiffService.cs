using QuantumBuild.Modules.ToolboxTalks.Application.Abstractions.Validation;

namespace QuantumBuild.Modules.ToolboxTalks.Infrastructure.Services.Validation;

/// <summary>
/// Produces a word-level diff between two strings using the
/// Longest Common Subsequence (LCS) algorithm.
/// </summary>
public class WordDiffService : IWordDiffService
{
    /// <inheritdoc />
    public WordDiffResult Diff(string original, string candidate)
    {
        var originalWords = Tokenise(original);
        var candidateWords = Tokenise(candidate);

        // Both empty
        if (originalWords.Length == 0 && candidateWords.Length == 0)
        {
            return new WordDiffResult { SimilarityPercentage = 100.0 };
        }

        // One side empty
        if (originalWords.Length == 0)
        {
            return new WordDiffResult
            {
                Operations = candidateWords.Select(w => new DiffOperation { Type = DiffType.Insert, Word = w }).ToList(),
                InsertedCount = candidateWords.Length,
                SimilarityPercentage = 0.0
            };
        }

        if (candidateWords.Length == 0)
        {
            return new WordDiffResult
            {
                Operations = originalWords.Select(w => new DiffOperation { Type = DiffType.Delete, Word = w }).ToList(),
                DeletedCount = originalWords.Length,
                SimilarityPercentage = 0.0
            };
        }

        // Build LCS table
        var m = originalWords.Length;
        var n = candidateWords.Length;
        var lcs = new int[m + 1, n + 1];

        for (var i = 1; i <= m; i++)
        {
            for (var j = 1; j <= n; j++)
            {
                if (string.Equals(originalWords[i - 1], candidateWords[j - 1], StringComparison.OrdinalIgnoreCase))
                    lcs[i, j] = lcs[i - 1, j - 1] + 1;
                else
                    lcs[i, j] = Math.Max(lcs[i - 1, j], lcs[i, j - 1]);
            }
        }

        // Back-track to produce diff operations
        var operations = new List<DiffOperation>();
        var oi = m;
        var ci = n;

        while (oi > 0 || ci > 0)
        {
            if (oi > 0 && ci > 0 &&
                string.Equals(originalWords[oi - 1], candidateWords[ci - 1], StringComparison.OrdinalIgnoreCase))
            {
                operations.Add(new DiffOperation { Type = DiffType.Equal, Word = originalWords[oi - 1] });
                oi--;
                ci--;
            }
            else if (ci > 0 && (oi == 0 || lcs[oi, ci - 1] >= lcs[oi - 1, ci]))
            {
                operations.Add(new DiffOperation { Type = DiffType.Insert, Word = candidateWords[ci - 1] });
                ci--;
            }
            else
            {
                operations.Add(new DiffOperation { Type = DiffType.Delete, Word = originalWords[oi - 1] });
                oi--;
            }
        }

        operations.Reverse();

        var matchingCount = operations.Count(o => o.Type == DiffType.Equal);
        var insertedCount = operations.Count(o => o.Type == DiffType.Insert);
        var deletedCount = operations.Count(o => o.Type == DiffType.Delete);
        var maxWords = Math.Max(m, n);
        var similarity = maxWords > 0 ? (double)matchingCount / maxWords * 100.0 : 100.0;

        return new WordDiffResult
        {
            Operations = operations,
            MatchingWordCount = matchingCount,
            InsertedCount = insertedCount,
            DeletedCount = deletedCount,
            SimilarityPercentage = similarity
        };
    }

    /// <summary>
    /// Splits text into words on whitespace.
    /// </summary>
    private static string[] Tokenise(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return [];

        return text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
    }
}
