namespace QuantumBuild.Modules.ToolboxTalks.Domain.Enums;

/// <summary>
/// Overall status of a subtitle processing job
/// </summary>
public enum SubtitleProcessingStatus
{
    /// <summary>
    /// Job is queued and waiting to start
    /// </summary>
    Pending = 1,

    /// <summary>
    /// Audio is being transcribed via ElevenLabs
    /// </summary>
    Transcribing = 2,

    /// <summary>
    /// Subtitles are being translated to target languages
    /// </summary>
    Translating = 3,

    /// <summary>
    /// SRT files are being uploaded to storage
    /// </summary>
    Uploading = 4,

    /// <summary>
    /// All processing completed successfully
    /// </summary>
    Completed = 5,

    /// <summary>
    /// Processing failed due to an error
    /// </summary>
    Failed = 6,

    /// <summary>
    /// Processing was cancelled by user
    /// </summary>
    Cancelled = 7
}
