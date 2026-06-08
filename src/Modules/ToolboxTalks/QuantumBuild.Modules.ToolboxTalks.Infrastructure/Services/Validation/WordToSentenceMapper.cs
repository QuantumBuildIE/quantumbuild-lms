using QuantumBuild.Modules.ToolboxTalks.Application.Abstractions.Validation;

namespace QuantumBuild.Modules.ToolboxTalks.Infrastructure.Services.Validation;

/// <summary>
/// Maps word-indexed diff runs to sentence-level character spans in the original text.
/// Uses the same whitespace-split tokenisation as WordDiffService so word indices align.
/// </summary>
public class WordToSentenceMapper : IWordToSentenceMapper
{
    /// <inheritdoc />
    public IReadOnlyList<(int StartOffset, int EndOffset)> Map(
        string originalText,
        IReadOnlyList<SentenceSpan> sentences,
        IReadOnlyList<DiffRun> diffRuns)
    {
        if (diffRuns.Count == 0 || sentences.Count == 0)
            return [];

        var wordCharMap = BuildWordCharMap(originalText);

        if (wordCharMap.Length == 0)
            return [];

        var seen = new HashSet<(int, int)>();
        var result = new List<(int StartOffset, int EndOffset)>();

        foreach (var run in diffRuns)
        {
            // Clamp to last word when the run's index falls past the end of the text
            int wordIndex = Math.Min(run.StartWordIndex, wordCharMap.Length - 1);
            int charPos = wordCharMap[wordIndex].Start;

            var sentence = FindSentence(sentences, charPos);
            var key = (sentence.Start, sentence.End);

            if (seen.Add(key))
                result.Add((sentence.Start, sentence.End));
        }

        return result;
    }

    /// <summary>
    /// Returns the sentence span containing <paramref name="charPos"/>.
    /// Falls back to the last sentence for positions outside all spans.
    /// </summary>
    private static SentenceSpan FindSentence(IReadOnlyList<SentenceSpan> sentences, int charPos)
    {
        for (int i = 0; i < sentences.Count; i++)
        {
            var s = sentences[i];
            bool inSpan = charPos >= s.Start && charPos < s.End;
            bool isLast = i == sentences.Count - 1;
            if (inSpan || (isLast && charPos >= s.Start))
                return s;
        }
        return sentences[sentences.Count - 1];
    }

    /// <summary>
    /// Builds an array of (StartChar, EndChar) pairs for each whitespace-delimited word,
    /// matching the tokenisation used by WordDiffService.
    /// </summary>
    private static (int Start, int End)[] BuildWordCharMap(string text)
    {
        if (string.IsNullOrEmpty(text))
            return [];

        var words = new List<(int Start, int End)>();
        int i = 0;

        while (i < text.Length)
        {
            while (i < text.Length && char.IsWhiteSpace(text[i]))
                i++;
            if (i >= text.Length)
                break;

            int wordStart = i;
            while (i < text.Length && !char.IsWhiteSpace(text[i]))
                i++;

            words.Add((wordStart, i));
        }

        return [.. words];
    }
}
