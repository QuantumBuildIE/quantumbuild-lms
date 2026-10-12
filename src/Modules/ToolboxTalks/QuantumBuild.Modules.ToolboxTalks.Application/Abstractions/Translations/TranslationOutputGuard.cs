using System.Text.RegularExpressions;

namespace QuantumBuild.Modules.ToolboxTalks.Application.Abstractions.Translations;

/// <summary>
/// Rejects model output that is not a clean translation (reasoning or preamble leaked into the
/// text, or output wildly longer than the source). Applied before translated content is persisted.
/// </summary>
public static class TranslationOutputGuard
{
    /// <summary>Phrases that never occur in a real translation: rejected wherever they appear.</summary>
    public static readonly IReadOnlyList<string> AnywhereMarkers = new[]
    {
        "The user wants",
        "Let me translate",
        "Wait, let me"
    };

    /// <summary>
    /// Phrases that can appear legitimately mid-sentence (e.g. "I need to"): rejected only at the
    /// start of the output or the start of a line.
    /// </summary>
    public static readonly IReadOnlyList<string> LineStartMarkers = new[]
    {
        "Let me ",
        "Wait,",
        "Looking at",
        "I need to",
        "Here is the translation",
        "Here's the translation",
        "Here’s the translation"
    };

    private static readonly Regex LineStartPattern = new(
        @"(?:^|\n)[ \t]*(" +string.Join("|", LineStartMarkers.Select(Regex.Escape)) + ")",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    /// <summary>Output longer than this multiple of the source length is rejected.</summary>
    public const double MaxLengthRatio = 2.5;

    /// <summary>
    /// The ratio check is skipped for very short sources, where a legitimate translation can be
    /// several times longer (e.g. "OK"). Marker checks still apply.
    /// </summary>
    public const int MinSourceLengthForRatio = 20;

    private static readonly Regex HtmlTagPattern = new(@"</?[a-zA-Z][a-zA-Z0-9]*(\s[^>]*)?/?>", RegexOptions.Compiled);

    /// <summary>True when the text contains at least one HTML tag.</summary>
    public static bool LooksLikeHtml(string text) =>
        !string.IsNullOrEmpty(text) && HtmlTagPattern.IsMatch(text);

    /// <summary>
    /// Returns null when the output is acceptable, otherwise a short reason for rejection.
    /// </summary>
    public static string? GetRejectionReason(string source, string output)
    {
        var lineStart = LineStartPattern.Match(output);
        if (lineStart.Success)
        {
            return $"starts a line with reasoning marker '{lineStart.Groups[1].Value.Trim()}'";
        }

        foreach (var marker in AnywhereMarkers)
        {
            if (output.Contains(marker, StringComparison.OrdinalIgnoreCase))
            {
                return $"contains reasoning marker '{marker.Trim()}'";
            }
        }

        if (source.Length >= MinSourceLengthForRatio && output.Length > source.Length * MaxLengthRatio)
        {
            return $"output length {output.Length} exceeds {MaxLengthRatio}x source length {source.Length}";
        }

        return null;
    }
}
