namespace QuantumBuild.Modules.ToolboxTalks.Application.Abstractions.Subtitles;

/// <summary>
/// Service for transcribing audio from video files using external APIs (e.g., ElevenLabs).
/// </summary>
public interface ITranscriptionService
{
    /// <summary>
    /// Transcribes audio from a video URL and returns word-level timing data.
    /// </summary>
    /// <param name="videoUrl">Direct URL to the video file</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Transcription result with word-level timing</returns>
    Task<TranscriptionResult> TranscribeAsync(string videoUrl, CancellationToken cancellationToken = default);
}

/// <summary>
/// Result of a transcription operation
/// </summary>
public class TranscriptionResult
{
    /// <summary>
    /// Whether the transcription was successful
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// Error message if transcription failed
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// List of transcribed words with timing information
    /// </summary>
    public List<TranscriptWord> Words { get; set; } = new();

    /// <summary>
    /// Raw JSON response from the transcription API (for debugging)
    /// </summary>
    public string? RawResponse { get; set; }

    /// <summary>
    /// Creates a successful result
    /// </summary>
    public static TranscriptionResult SuccessResult(List<TranscriptWord> words, string? rawResponse = null) =>
        new()
        {
            Success = true,
            Words = words,
            RawResponse = rawResponse
        };

    /// <summary>
    /// Creates a failed result
    /// </summary>
    public static TranscriptionResult FailureResult(string errorMessage) =>
        new()
        {
            Success = false,
            ErrorMessage = errorMessage
        };
}

/// <summary>
/// Represents a single word from transcription with timing data
/// </summary>
public class TranscriptWord
{
    /// <summary>
    /// The transcribed text (word, punctuation, etc.)
    /// </summary>
    public string Text { get; set; } = string.Empty;

    /// <summary>
    /// Type of element: "word", "spacing", "punctuation", "audio_event"
    /// </summary>
    public string Type { get; set; } = string.Empty;

    /// <summary>
    /// Start time in seconds from the beginning of the video
    /// </summary>
    public decimal Start { get; set; }

    /// <summary>
    /// End time in seconds from the beginning of the video
    /// </summary>
    public decimal End { get; set; }
}
