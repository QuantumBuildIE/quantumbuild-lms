namespace QuantumBuild.Modules.LessonParser.Application.Abstractions;

/// <summary>
/// Queues content translations for generated talks using the existing
/// ToolboxTalks translation pipeline (GenerateContentTranslationsCommand).
/// </summary>
public interface ITranslationQueueService
{
    /// <summary>
    /// Identifies required languages from tenant employee preferences and dispatches
    /// translation commands for each generated talk. Optionally translates the course
    /// title and description into ToolboxTalkCourseTranslation records.
    /// </summary>
    /// <param name="talkIds">IDs of the generated ToolboxTalk entities</param>
    /// <param name="tenantId">Tenant ID for scoping employee language queries</param>
    /// <param name="courseId">Optional course ID — if provided, course title/description are also translated</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Result with counts and language codes</returns>
    Task<TranslationQueueResult> QueueTranslationsForTalksAsync(
        IEnumerable<Guid> talkIds,
        Guid tenantId,
        Guid? courseId = null,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Result of queuing translations for generated talks
/// </summary>
public record TranslationQueueResult
{
    /// <summary>
    /// Total number of translation jobs dispatched (talks × languages)
    /// </summary>
    public int JobsQueued { get; init; }

    /// <summary>
    /// Language codes that translations were queued for
    /// </summary>
    public IReadOnlyList<string> LanguageCodes { get; init; } = [];

    /// <summary>
    /// Whether any languages needed translation (false = all employees use source language)
    /// </summary>
    public bool HasLanguagesToTranslate { get; init; }

    /// <summary>
    /// JSON array of failures if any translations failed, null if all succeeded
    /// </summary>
    public string? FailuresJson { get; init; }

    /// <summary>
    /// Whether some but not all translations failed
    /// </summary>
    public bool HasPartialFailures { get; init; }

    /// <summary>
    /// Whether all translations failed
    /// </summary>
    public bool AllFailed { get; init; }
}
