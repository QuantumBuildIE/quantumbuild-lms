using System.Text.RegularExpressions;

namespace QuantumBuild.Modules.ToolboxTalks.Infrastructure.Services;

/// <summary>
/// Backstop sanitizer for AI-generated learning content. Removes residual `[m:ss]`/`[h:mm:ss]`
/// caption-timestamp markers (the format produced by TranscriptService.FormatTimestamp) that an
/// LLM may echo into generated section content despite prompt instructions to exclude them.
/// Source transcript text should already be timestamp-free by the time it reaches the model
/// (see ITranscriptService.GetCleanFullText) - this is a defence-in-depth backstop only.
/// </summary>
public static class TranscriptMarkerSanitizer
{
    private static readonly Regex TimestampMarkerPattern = new(
        @"\[\d{1,3}:\d{2}(?::\d{2})?\]\s*",
        RegexOptions.Compiled);

    /// <summary>
    /// Strips `[m:ss]`/`[h:mm:ss]` timestamp markers from content, trimming any whitespace left
    /// behind. Returns the input unchanged if it contains no markers.
    /// </summary>
    public static string StripTimestampMarkers(string? content)
    {
        if (string.IsNullOrEmpty(content))
        {
            return content ?? string.Empty;
        }

        return TimestampMarkerPattern.Replace(content, string.Empty).Trim();
    }
}
