using QuantumBuild.Core.Domain.Common;
using QuantumBuild.Modules.ToolboxTalks.Domain.Enums;

namespace QuantumBuild.Modules.ToolboxTalks.Domain.Entities;

/// <summary>
/// Tracks the state of a content creation wizard session.
/// Manages the pipeline from raw input through parsing, translation, validation, and publishing.
/// </summary>
public class ContentCreationSession : TenantEntity
{
    // Input configuration
    public InputMode InputMode { get; set; }
    public ContentCreationSessionStatus Status { get; set; } = ContentCreationSessionStatus.Draft;

    // Source content — Text mode
    public string? SourceText { get; set; }

    // Source content — File mode (Pdf/Video)
    public string? SourceFileName { get; set; }
    public string? SourceFileUrl { get; set; }
    public string? SourceFileType { get; set; }

    // Transcript — populated after video transcription
    public string? TranscriptText { get; set; }

    // Parsed content — JSON array of parsed sections
    public string? ParsedSectionsJson { get; set; }

    // Output configuration
    public OutputType? OutputType { get; set; }
    public Guid? OutputTalkId { get; set; }
    public Guid? OutputCourseId { get; set; }

    // Translation & validation
    public string? TargetLanguageCodes { get; set; }
    public int PassThreshold { get; set; } = 75;
    public string? SectorKey { get; set; }

    // Audit metadata
    public string? ReviewerName { get; set; }
    public string? ReviewerOrg { get; set; }
    public string? ReviewerRole { get; set; }
    public string? DocumentRef { get; set; }
    public string? ClientName { get; set; }
    public string? AuditPurpose { get; set; }

    // Session lifecycle
    public DateTime ExpiresAt { get; set; }

    // Validation run tracking — JSON array of run IDs created for this session
    public string? ValidationRunIds { get; set; }

    // Quiz — JSON arrays of generated/edited questions and quiz settings
    public string? QuestionsJson { get; set; }
    public string? QuizSettingsJson { get; set; }

    // Settings — JSON object of publish/behaviour settings (title, category, refresher, etc.)
    public string? SettingsJson { get; set; }

    // Subtitle processing — tracks the subtitle job spawned during content creation
    public string? SubtitleJobId { get; set; }

    // Navigation
    public ToolboxTalk? OutputTalk { get; set; }
    public ToolboxTalkCourse? OutputCourse { get; set; }
}
