namespace QuantumBuild.Modules.ToolboxTalks.Application.Services;

/// <summary>
/// Service for detecting and handling duplicate content in toolbox talks.
/// Calculates file hashes and manages content reuse from existing toolbox talks.
/// </summary>
public interface IContentDeduplicationService
{
    /// <summary>
    /// Calculates the SHA-256 hash of a file from a stream.
    /// </summary>
    /// <param name="fileStream">The file stream to hash</param>
    /// <returns>The SHA-256 hash as a hex string (64 characters)</returns>
    string CalculateFileHash(Stream fileStream);

    /// <summary>
    /// Calculates the SHA-256 hash of a file from a URL.
    /// Downloads the file temporarily to calculate the hash.
    /// </summary>
    /// <param name="fileUrl">The URL of the file to hash</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The SHA-256 hash as a hex string (64 characters)</returns>
    Task<string> CalculateFileHashFromUrlAsync(string fileUrl, CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks if there's an existing toolbox talk with the same file hash.
    /// </summary>
    /// <param name="tenantId">The tenant ID to search within</param>
    /// <param name="fileHash">The file hash to look for</param>
    /// <param name="fileType">Whether this is a PDF or Video file</param>
    /// <param name="excludeToolboxTalkId">ID of the toolbox talk to exclude from the search</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Duplicate check result with source toolbox talk info if found</returns>
    Task<DuplicateCheckResult> CheckForDuplicateAsync(
        Guid tenantId,
        string fileHash,
        FileHashType fileType,
        Guid excludeToolboxTalkId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Copies all generated content (sections, questions, translations) from a source toolbox talk to a target.
    /// </summary>
    /// <param name="targetToolboxTalkId">The toolbox talk to copy content into</param>
    /// <param name="sourceToolboxTalkId">The toolbox talk to copy content from</param>
    /// <param name="tenantId">The tenant ID (for security validation)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Result of the reuse operation</returns>
    Task<ContentReuseResult> ReuseContentAsync(
        Guid targetToolboxTalkId,
        Guid sourceToolboxTalkId,
        Guid tenantId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Copies selected content types from a source toolbox talk to a target (partial reuse).
    /// </summary>
    /// <param name="targetToolboxTalkId">The toolbox talk to copy content into</param>
    /// <param name="sourceToolboxTalkId">The toolbox talk to copy content from</param>
    /// <param name="tenantId">The tenant ID (for security validation)</param>
    /// <param name="options">Which content types to copy</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Result of the reuse operation</returns>
    Task<ContentReuseResult> ReuseContentAsync(
        Guid targetToolboxTalkId,
        Guid sourceToolboxTalkId,
        Guid tenantId,
        ReuseContentOptions options,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates the file hash for a toolbox talk.
    /// </summary>
    /// <param name="toolboxTalkId">The toolbox talk ID</param>
    /// <param name="fileHash">The calculated file hash</param>
    /// <param name="fileType">Whether this is a PDF or Video file</param>
    /// <param name="tenantId">The tenant ID (for security validation)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task UpdateFileHashAsync(
        Guid toolboxTalkId,
        string fileHash,
        FileHashType fileType,
        Guid tenantId,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Type of file for hash identification
/// </summary>
public enum FileHashType
{
    /// <summary>PDF document</summary>
    Pdf,
    /// <summary>Video file</summary>
    Video
}

/// <summary>
/// Result of a duplicate check operation
/// </summary>
public record DuplicateCheckResult
{
    /// <summary>Whether a duplicate was found</summary>
    public bool IsDuplicate { get; init; }

    /// <summary>Information about the source toolbox talk if a duplicate was found</summary>
    public SourceToolboxTalkInfo? SourceToolboxTalk { get; init; }
}

/// <summary>
/// Information about a source toolbox talk for content reuse
/// </summary>
public record SourceToolboxTalkInfo
{
    /// <summary>ID of the source toolbox talk</summary>
    public Guid Id { get; init; }

    /// <summary>Title of the source toolbox talk</summary>
    public string Title { get; init; } = string.Empty;

    /// <summary>When the content was originally generated</summary>
    public DateTime? ProcessedAt { get; init; }

    /// <summary>Number of sections in the source</summary>
    public int SectionCount { get; init; }

    /// <summary>Number of questions in the source</summary>
    public int QuestionCount { get; init; }

    /// <summary>Whether the source has an HTML slideshow</summary>
    public bool HasSlideshow { get; init; }

    /// <summary>Whether the source has completed subtitle processing</summary>
    public bool HasSubtitles { get; init; }

    /// <summary>Languages that have translations available</summary>
    public List<string> TranslationLanguages { get; init; } = new();
}

/// <summary>
/// Options for controlling which content types to copy during reuse
/// </summary>
public record ReuseContentOptions
{
    /// <summary>Whether to copy sections</summary>
    public bool CopySections { get; init; } = true;

    /// <summary>Whether to copy questions</summary>
    public bool CopyQuestions { get; init; } = true;

    /// <summary>Whether to copy the HTML slideshow</summary>
    public bool CopySlideshow { get; init; } = true;

    /// <summary>Whether to copy translations</summary>
    public bool CopyTranslations { get; init; } = true;
}

/// <summary>
/// Result of a content reuse operation
/// </summary>
public record ContentReuseResult
{
    /// <summary>Whether the reuse was successful</summary>
    public bool Success { get; init; }

    /// <summary>Error message if the reuse failed</summary>
    public string? ErrorMessage { get; init; }

    /// <summary>Number of sections copied</summary>
    public int SectionsCopied { get; init; }

    /// <summary>Number of questions copied</summary>
    public int QuestionsCopied { get; init; }

    /// <summary>Whether the HTML slideshow was copied</summary>
    public bool SlideshowCopied { get; init; }

    /// <summary>Number of translation languages copied</summary>
    public int TranslationsCopied { get; init; }

    /// <summary>Whether subtitle data (processing job + translations) was copied</summary>
    public bool SubtitlesCopied { get; init; }
}
