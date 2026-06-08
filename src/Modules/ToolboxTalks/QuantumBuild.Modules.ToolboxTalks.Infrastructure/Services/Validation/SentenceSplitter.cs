using System.Text.RegularExpressions;
using QuantumBuild.Modules.ToolboxTalks.Application.Abstractions.Validation;

namespace QuantumBuild.Modules.ToolboxTalks.Infrastructure.Services.Validation;

/// <summary>
/// English sentence splitter using regex-based boundary detection with an abbreviation guard list.
/// </summary>
public class SentenceSplitter : ISentenceSplitter
{
    // One or more sentence terminators followed by whitespace or end-of-string.
    // The lookahead consumes no characters, so the End of each span includes the terminators.
    private static readonly Regex TerminatorPattern =
        new(@"[.?!]+(?=\s|$)", RegexOptions.Compiled);

    private static readonly string[] Abbreviations =
    [
        "Dr.", "Mr.", "Mrs.", "Ms.", "St.", "vs.", "etc.", "e.g.", "i.e.",
        "approx.", "no.", "fig.", "Inc.", "Ltd.", "Co.", "Corp.", "Jr.", "Sr."
    ];

    /// <inheritdoc />
    public IReadOnlyList<SentenceSpan> Split(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return [];

        var result = new List<SentenceSpan>();
        var matches = TerminatorPattern.Matches(text);
        int sentenceStart = 0;

        foreach (Match match in matches)
        {
            // Skip if the terminator group is part of a known abbreviation
            if (IsAbbreviation(text, match.Index + match.Length))
                continue;

            int sentenceEnd = match.Index + match.Length;
            result.Add(new SentenceSpan(sentenceStart, sentenceEnd));

            // Advance past whitespace to find the start of the next sentence
            int next = sentenceEnd;
            while (next < text.Length && char.IsWhiteSpace(text[next]))
                next++;
            sentenceStart = next;
        }

        // Any remaining text with no trailing terminator forms the last sentence
        if (sentenceStart < text.Length)
            result.Add(new SentenceSpan(sentenceStart, text.Length));

        return result;
    }

    /// <summary>
    /// Returns true when the text immediately before <paramref name="endPosition"/> ends with
    /// a known abbreviation, indicating the terminator is not a sentence boundary.
    /// </summary>
    private static bool IsAbbreviation(string text, int endPosition)
    {
        foreach (var abbrev in Abbreviations)
        {
            if (endPosition >= abbrev.Length &&
                text.AsSpan(endPosition - abbrev.Length, abbrev.Length)
                    .Equals(abbrev, StringComparison.OrdinalIgnoreCase))
                return true;
        }
        return false;
    }
}
