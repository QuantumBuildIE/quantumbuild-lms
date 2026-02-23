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
}
