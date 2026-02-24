using QuantumBuild.Core.Domain.Common;
using QuantumBuild.Modules.LessonParser.Domain.Enums;

namespace QuantumBuild.Modules.LessonParser.Domain.Entities;

/// <summary>
/// Represents a lesson parsing job that processes a document into a course with talks.
/// Tracks the input source, processing status, and generated output.
/// </summary>
public class ParseJob : TenantEntity
{
    /// <summary>
    /// Type of input document (PDF, DOCX, URL, or Text)
    /// </summary>
    public ParseInputType InputType { get; set; }

    /// <summary>
    /// Reference to the input source — filename or URL, never raw content
    /// </summary>
    public string InputReference { get; set; } = string.Empty;

    /// <summary>
    /// Current processing status of the job
    /// </summary>
    public ParseJobStatus Status { get; set; }

    /// <summary>
    /// FK to ToolboxTalkCourse — the course generated from parsing (no navigation property)
    /// </summary>
    public Guid? GeneratedCourseId { get; set; }

    /// <summary>
    /// Snapshot of the generated course title in case the course is later deleted
    /// </summary>
    public string? GeneratedCourseTitle { get; set; }

    /// <summary>
    /// Number of toolbox talks generated from this parse job
    /// </summary>
    public int TalksGenerated { get; set; }

    /// <summary>
    /// Error message if the job failed
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// Temporarily stores extracted text content for retry scenarios.
    /// Cleared on successful completion to save storage.
    /// Failed jobs retain this so they can be retried without re-uploading.
    /// </summary>
    public string? ExtractedContent { get; set; }

    /// <summary>
    /// Status of translation queuing after successful generation
    /// </summary>
    public TranslationQueueStatus TranslationStatus { get; set; } = TranslationQueueStatus.NotRequired;

    /// <summary>
    /// Comma-separated language codes that were queued for translation, e.g. "es,fr,de"
    /// </summary>
    public string? TranslationLanguages { get; set; }

    /// <summary>
    /// JSON array of failed translations with language and reason, populated after translation completes
    /// </summary>
    public string? TranslationFailures { get; set; }

    /// <summary>
    /// Total number of translation jobs enqueued (talk × language combinations)
    /// </summary>
    public int TranslationsQueued { get; set; }
}
