namespace QuantumBuild.Modules.ToolboxTalks.Application.Abstractions.Subtitles;

/// <summary>
/// Service for retrieving and parsing video transcripts for AI content generation.
/// Converts SRT subtitle files into structured transcript data with timing information.
/// </summary>
public interface ITranscriptService
{
    /// <summary>
    /// Gets the transcript for a toolbox talk video, with timing information.
    /// Retrieves the English SRT file and parses it into structured segments.
    /// </summary>
    /// <param name="toolboxTalkId">The toolbox talk ID</param>
    /// <param name="totalVideoDuration">Optional total video duration for accurate percentage calculation</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Transcript result with segments and timing data</returns>
    Task<TranscriptResult> GetTranscriptAsync(
        Guid toolboxTalkId,
        TimeSpan? totalVideoDuration = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Parses SRT content into structured transcript segments.
    /// </summary>
    /// <param name="srtContent">Raw SRT file content</param>
    /// <param name="totalDuration">Optional total video duration for percentage calculation</param>
    /// <returns>Parsed transcript result</returns>
    TranscriptResult ParseSrtContent(string srtContent, TimeSpan? totalDuration = null);

    /// <summary>
    /// Gets transcript segments from the final portion of the video.
    /// Useful for generating quiz questions that ensure employees watch the entire video.
    /// </summary>
    /// <param name="transcript">The full transcript result</param>
    /// <param name="startPercentage">Start percentage (0-100), default is 80%</param>
    /// <returns>List of segments from the specified portion onwards</returns>
    List<TranscriptSegment> GetFinalPortionSegments(TranscriptResult transcript, decimal startPercentage = 80);

    /// <summary>
    /// Gets the text content from a specific percentage range of the video.
    /// </summary>
    /// <param name="transcript">The full transcript result</param>
    /// <param name="startPercentage">Start percentage (0-100)</param>
    /// <param name="endPercentage">End percentage (0-100)</param>
    /// <returns>Combined text from segments in the specified range</returns>
    string GetTextForPercentageRange(TranscriptResult transcript, decimal startPercentage, decimal endPercentage);

    /// <summary>
    /// Formats the transcript with timestamps for AI consumption.
    /// Each line includes a timestamp prefix for context.
    /// </summary>
    /// <param name="transcript">The transcript result to format</param>
    /// <returns>Formatted transcript text with timestamps</returns>
    string FormatForAi(TranscriptResult transcript);
}

/// <summary>
/// Result of a transcript retrieval or parsing operation.
/// </summary>
/// <param name="Success">Whether the operation was successful</param>
/// <param name="FullText">Complete transcript text with timestamps (formatted for AI)</param>
/// <param name="Segments">Individual transcript segments with timing data</param>
/// <param name="TotalDuration">Total duration of the video/transcript</param>
/// <param name="ErrorMessage">Error message if the operation failed</param>
public record TranscriptResult(
    bool Success,
    string? FullText,
    List<TranscriptSegment> Segments,
    TimeSpan? TotalDuration,
    string? ErrorMessage)
{
    /// <summary>
    /// Creates a successful transcript result.
    /// </summary>
    public static TranscriptResult SuccessResult(
        string fullText,
        List<TranscriptSegment> segments,
        TimeSpan totalDuration) =>
        new(
            Success: true,
            FullText: fullText,
            Segments: segments,
            TotalDuration: totalDuration,
            ErrorMessage: null);

    /// <summary>
    /// Creates a failed transcript result.
    /// </summary>
    public static TranscriptResult FailureResult(string errorMessage) =>
        new(
            Success: false,
            FullText: null,
            Segments: new List<TranscriptSegment>(),
            TotalDuration: null,
            ErrorMessage: errorMessage);
}

/// <summary>
/// Represents a single segment of transcript text with timing information.
/// </summary>
/// <param name="Index">Sequential index of the segment (1-based, from SRT)</param>
/// <param name="StartTime">Start time of the segment</param>
/// <param name="EndTime">End time of the segment</param>
/// <param name="Text">The transcript text for this segment</param>
/// <param name="PercentageIntoVideo">Position as percentage into video (0-100)</param>
public record TranscriptSegment(
    int Index,
    TimeSpan StartTime,
    TimeSpan EndTime,
    string Text,
    decimal PercentageIntoVideo);
