using System.Text;
using Microsoft.Extensions.Logging;
using QuantumBuild.Modules.ToolboxTalks.Application.Abstractions.Subtitles;
using QuantumBuild.Modules.ToolboxTalks.Application.Services.Subtitles;

namespace QuantumBuild.Modules.ToolboxTalks.Infrastructure.Services.Subtitles;

/// <summary>
/// Service for retrieving and parsing video transcripts from SRT subtitle files.
/// Provides structured transcript data with timing information for AI content generation.
/// </summary>
public class TranscriptService : ITranscriptService
{
    private readonly ISubtitleProcessingOrchestrator _orchestrator;
    private readonly ILogger<TranscriptService> _logger;

    public TranscriptService(
        ISubtitleProcessingOrchestrator orchestrator,
        ILogger<TranscriptService> logger)
    {
        _orchestrator = orchestrator;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<TranscriptResult> GetTranscriptAsync(
        Guid toolboxTalkId,
        TimeSpan? totalVideoDuration = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // Try to get English SRT first (primary transcript language)
            var srtContent = await _orchestrator.GetSrtContentAsync(toolboxTalkId, "en", cancellationToken);

            if (string.IsNullOrWhiteSpace(srtContent))
            {
                _logger.LogWarning(
                    "No English transcript found for toolbox talk {ToolboxTalkId}. Please generate subtitles first.",
                    toolboxTalkId);

                return TranscriptResult.FailureResult(
                    "No English transcript found. Please generate subtitles first.");
            }

            return ParseSrtContent(srtContent, totalVideoDuration);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Failed to retrieve transcript for toolbox talk {ToolboxTalkId}",
                toolboxTalkId);

            return TranscriptResult.FailureResult($"Failed to retrieve transcript: {ex.Message}");
        }
    }

    /// <inheritdoc />
    public TranscriptResult ParseSrtContent(string srtContent, TimeSpan? totalDuration = null)
    {
        if (string.IsNullOrWhiteSpace(srtContent))
        {
            return TranscriptResult.FailureResult("SRT content is empty.");
        }

        try
        {
            var segments = new List<TranscriptSegment>();
            var fullTextBuilder = new StringBuilder();

            // SRT format:
            // 1
            // 00:00:01,000 --> 00:00:04,000
            // Text content here
            //
            // 2
            // ...

            // Normalize line endings and split by double newlines
            var normalizedContent = srtContent.Replace("\r\n", "\n").Replace("\r", "\n");
            var blocks = normalizedContent.Split(new[] { "\n\n" }, StringSplitOptions.RemoveEmptyEntries);

            foreach (var block in blocks)
            {
                var lines = block.Split('\n', StringSplitOptions.RemoveEmptyEntries);

                if (lines.Length < 3) continue;

                // Line 0: Index
                if (!int.TryParse(lines[0].Trim(), out var index)) continue;

                // Line 1: Timestamps (00:00:01,000 --> 00:00:04,000)
                var timestampLine = lines[1].Trim();
                var timeParts = timestampLine.Split(" --> ");
                if (timeParts.Length != 2) continue;

                var startTime = ParseSrtTimestamp(timeParts[0].Trim());
                var endTime = ParseSrtTimestamp(timeParts[1].Trim());

                if (startTime == null || endTime == null) continue;

                // Lines 2+: Text content (may span multiple lines)
                var text = string.Join(" ", lines.Skip(2).Select(l => l.Trim()));

                // Skip empty text segments
                if (string.IsNullOrWhiteSpace(text)) continue;

                fullTextBuilder.AppendLine($"[{FormatTimestamp(startTime.Value)}] {text}");

                segments.Add(new TranscriptSegment(
                    Index: index,
                    StartTime: startTime.Value,
                    EndTime: endTime.Value,
                    Text: text,
                    PercentageIntoVideo: 0 // Will be calculated after we know total duration
                ));
            }

            if (segments.Count == 0)
            {
                return TranscriptResult.FailureResult("No valid transcript segments found in SRT content.");
            }

            // Calculate total duration from last segment if not provided
            var actualDuration = totalDuration ?? segments.Max(s => s.EndTime);

            // Calculate percentage for each segment based on start time
            var segmentsWithPercentage = segments.Select(s => s with
            {
                PercentageIntoVideo = actualDuration.TotalSeconds > 0
                    ? Math.Round((decimal)(s.StartTime.TotalSeconds / actualDuration.TotalSeconds * 100), 2)
                    : 0
            }).ToList();

            _logger.LogDebug(
                "Parsed SRT content into {SegmentCount} segments, total duration: {Duration}",
                segmentsWithPercentage.Count,
                actualDuration);

            return TranscriptResult.SuccessResult(
                fullTextBuilder.ToString(),
                segmentsWithPercentage,
                actualDuration);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to parse SRT content");
            return TranscriptResult.FailureResult($"Failed to parse SRT content: {ex.Message}");
        }
    }

    /// <inheritdoc />
    public List<TranscriptSegment> GetFinalPortionSegments(TranscriptResult transcript, decimal startPercentage = 80)
    {
        if (!transcript.Success || transcript.Segments.Count == 0)
        {
            return new List<TranscriptSegment>();
        }

        return transcript.Segments
            .Where(s => s.PercentageIntoVideo >= startPercentage)
            .OrderBy(s => s.Index)
            .ToList();
    }

    /// <inheritdoc />
    public string GetTextForPercentageRange(TranscriptResult transcript, decimal startPercentage, decimal endPercentage)
    {
        if (!transcript.Success || transcript.Segments.Count == 0)
        {
            return string.Empty;
        }

        var relevantSegments = transcript.Segments
            .Where(s => s.PercentageIntoVideo >= startPercentage && s.PercentageIntoVideo <= endPercentage)
            .OrderBy(s => s.Index);

        return string.Join(" ", relevantSegments.Select(s => s.Text));
    }

    /// <inheritdoc />
    public string FormatForAi(TranscriptResult transcript)
    {
        if (!transcript.Success || transcript.Segments.Count == 0)
        {
            return string.Empty;
        }

        var builder = new StringBuilder();
        builder.AppendLine("=== VIDEO TRANSCRIPT ===");
        builder.AppendLine($"Total Duration: {FormatDuration(transcript.TotalDuration ?? TimeSpan.Zero)}");
        builder.AppendLine();

        foreach (var segment in transcript.Segments.OrderBy(s => s.Index))
        {
            builder.AppendLine($"[{FormatTimestamp(segment.StartTime)} - {segment.PercentageIntoVideo:F1}%] {segment.Text}");
        }

        builder.AppendLine();
        builder.AppendLine("=== END TRANSCRIPT ===");

        return builder.ToString();
    }

    /// <summary>
    /// Parses an SRT timestamp (00:00:01,000 or 00:00:01.000) into a TimeSpan.
    /// </summary>
    private static TimeSpan? ParseSrtTimestamp(string timestamp)
    {
        // SRT format uses comma for milliseconds: 00:00:01,000
        // Some files may use period: 00:00:01.000
        timestamp = timestamp.Replace(',', '.');

        // Try parsing as TimeSpan directly (handles hh:mm:ss.fff format)
        if (TimeSpan.TryParse(timestamp, out var result))
        {
            return result;
        }

        // Try manual parsing for edge cases
        var parts = timestamp.Split(':');
        if (parts.Length != 3)
        {
            return null;
        }

        if (!int.TryParse(parts[0], out var hours) ||
            !int.TryParse(parts[1], out var minutes))
        {
            return null;
        }

        // Seconds may include milliseconds
        var secondsParts = parts[2].Split('.');
        if (!int.TryParse(secondsParts[0], out var seconds))
        {
            return null;
        }

        var milliseconds = 0;
        if (secondsParts.Length > 1)
        {
            var msString = secondsParts[1].PadRight(3, '0').Substring(0, 3);
            int.TryParse(msString, out milliseconds);
        }

        return new TimeSpan(0, hours, minutes, seconds, milliseconds);
    }

    /// <summary>
    /// Formats a timestamp for display (e.g., "2:30" or "1:02:15").
    /// </summary>
    private static string FormatTimestamp(TimeSpan ts)
    {
        return ts.TotalHours >= 1
            ? $"{(int)ts.TotalHours}:{ts.Minutes:D2}:{ts.Seconds:D2}"
            : $"{ts.Minutes}:{ts.Seconds:D2}";
    }

    /// <summary>
    /// Formats a duration for display (e.g., "5 minutes 30 seconds").
    /// </summary>
    private static string FormatDuration(TimeSpan ts)
    {
        if (ts.TotalHours >= 1)
        {
            return $"{(int)ts.TotalHours} hour(s) {ts.Minutes} minute(s)";
        }

        if (ts.TotalMinutes >= 1)
        {
            return $"{(int)ts.TotalMinutes} minute(s) {ts.Seconds} second(s)";
        }

        return $"{ts.Seconds} second(s)";
    }
}
