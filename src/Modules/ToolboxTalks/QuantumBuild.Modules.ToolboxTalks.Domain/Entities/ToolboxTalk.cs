using QuantumBuild.Core.Domain.Common;
using QuantumBuild.Modules.ToolboxTalks.Domain.Enums;

namespace QuantumBuild.Modules.ToolboxTalks.Domain.Entities;

/// <summary>
/// Represents a toolbox talk - a safety briefing or training session
/// that employees must complete, optionally with video content and quiz assessment
/// </summary>
public class ToolboxTalk : TenantEntity
{
    /// <summary>
    /// Short unique code for the toolbox talk (e.g., "MHS-001")
    /// </summary>
    public string Code { get; set; } = string.Empty;

    /// <summary>
    /// Title of the toolbox talk
    /// </summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// Detailed description of the toolbox talk content and purpose
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Safety category this toolbox talk falls under (e.g., "Fire Safety", "Working at Heights")
    /// </summary>
    public string? Category { get; set; }

    /// <summary>
    /// How often employees must complete this toolbox talk
    /// </summary>
    public ToolboxTalkFrequency Frequency { get; set; } = ToolboxTalkFrequency.Once;

    /// <summary>
    /// URL to the video content (if any)
    /// </summary>
    public string? VideoUrl { get; set; }

    /// <summary>
    /// Source platform for the video
    /// </summary>
    public VideoSource VideoSource { get; set; } = VideoSource.None;

    /// <summary>
    /// URL to any attachment (PDF, document, etc.)
    /// </summary>
    public string? AttachmentUrl { get; set; }

    /// <summary>
    /// Minimum percentage of video that must be watched to mark as complete (0-100)
    /// Default is 90%
    /// </summary>
    public int MinimumVideoWatchPercent { get; set; } = 90;

    /// <summary>
    /// Whether a quiz must be passed to complete this toolbox talk
    /// </summary>
    public bool RequiresQuiz { get; set; } = false;

    /// <summary>
    /// Minimum score (percentage) required to pass the quiz
    /// Only applicable if RequiresQuiz is true
    /// </summary>
    public int? PassingScore { get; set; } = 80;

    /// <summary>
    /// Whether this toolbox talk is currently active and available to employees
    /// </summary>
    public bool IsActive { get; set; } = true;

    /// <summary>
    /// Current status in the content creation workflow
    /// </summary>
    public ToolboxTalkStatus Status { get; set; } = ToolboxTalkStatus.Draft;

    /// <summary>
    /// URL to the PDF document (if any)
    /// </summary>
    public string? PdfUrl { get; set; }

    /// <summary>
    /// Original filename of the uploaded PDF
    /// </summary>
    public string? PdfFileName { get; set; }

    /// <summary>
    /// Indicates if sections/questions were AI-generated from video content
    /// </summary>
    public bool GeneratedFromVideo { get; set; } = false;

    /// <summary>
    /// Indicates if sections/questions were AI-generated from PDF content
    /// </summary>
    public bool GeneratedFromPdf { get; set; } = false;

    /// <summary>
    /// Extracted text content from the PDF document.
    /// Used as input for AI generation of sections and quiz questions.
    /// </summary>
    public string? ExtractedPdfText { get; set; }

    /// <summary>
    /// When the PDF text was extracted. Used to determine if re-extraction is needed.
    /// </summary>
    public DateTime? PdfTextExtractedAt { get; set; }

    /// <summary>
    /// Extracted transcript text from the video (from SRT subtitles).
    /// Used as input for AI generation of sections and quiz questions.
    /// Includes timestamps for identifying content from different portions of the video.
    /// </summary>
    public string? ExtractedVideoTranscript { get; set; }

    /// <summary>
    /// When the video transcript was extracted. Used to determine if re-extraction is needed.
    /// </summary>
    public DateTime? VideoTranscriptExtractedAt { get; set; }

    /// <summary>
    /// SHA-256 hash of the PDF file content.
    /// Used for deduplication to detect identical files across toolbox talks.
    /// </summary>
    public string? PdfFileHash { get; set; }

    /// <summary>
    /// SHA-256 hash of the video file content.
    /// Used for deduplication to detect identical files across toolbox talks.
    /// </summary>
    public string? VideoFileHash { get; set; }

    /// <summary>
    /// Timestamp when content was generated for this toolbox talk.
    /// Used for duplicate detection to show when original content was processed.
    /// </summary>
    public DateTime? ContentGeneratedAt { get; set; }

    // Quiz randomization settings

    /// <summary>
    /// Number of questions to include in each quiz attempt.
    /// Null means use all questions. When UseQuestionPool is true,
    /// a random subset of this size is selected from the full question pool.
    /// </summary>
    public int? QuizQuestionCount { get; set; }

    /// <summary>
    /// Whether to shuffle the order of questions for each quiz attempt
    /// </summary>
    public bool ShuffleQuestions { get; set; } = false;

    /// <summary>
    /// Whether to shuffle the order of answer options within each question
    /// </summary>
    public bool ShuffleOptions { get; set; } = false;

    /// <summary>
    /// Whether to use question pool mode (random subset selection).
    /// When true, QuizQuestionCount questions are randomly selected from the full pool.
    /// Requires at least 2x QuizQuestionCount questions in the pool.
    /// </summary>
    public bool UseQuestionPool { get; set; } = false;

    // Certificate settings (Phase 5)

    /// <summary>
    /// Whether to generate a certificate on completion of this talk
    /// </summary>
    public bool GenerateCertificate { get; set; } = false;

    // Refresher settings (Phase 4)

    /// <summary>
    /// Whether completing this talk should auto-schedule a refresher
    /// </summary>
    public bool RequiresRefresher { get; set; } = false;

    /// <summary>
    /// Number of months after completion before refresher is due
    /// </summary>
    public int RefresherIntervalMonths { get; set; } = 12;

    // Auto-assignment settings

    /// <summary>
    /// Whether to automatically assign this talk to new employees
    /// </summary>
    public bool AutoAssignToNewEmployees { get; set; } = false;

    /// <summary>
    /// Number of days after hire date before the training is due
    /// </summary>
    public int AutoAssignDueDays { get; set; } = 14;

    // PDF slideshow settings

    /// <summary>
    /// Whether to generate slide images from the PDF for slideshow display
    /// </summary>
    public bool GenerateSlidesFromPdf { get; set; } = false;

    /// <summary>
    /// Whether the slide images have been generated from the PDF
    /// </summary>
    public bool SlidesGenerated { get; set; } = false;

    /// <summary>
    /// The AI-generated HTML slideshow content (in source language).
    /// Self-contained HTML with embedded CSS and JS.
    /// </summary>
    public string? SlideshowHtml { get; set; }

    /// <summary>
    /// When the HTML slideshow was generated.
    /// </summary>
    public DateTime? SlideshowGeneratedAt { get; set; }

    /// <summary>
    /// The language code of the original content (e.g., "en", "es", "af").
    /// Used as source language for translations.
    /// </summary>
    public string SourceLanguageCode { get; set; } = "en";

    // Navigation properties

    /// <summary>
    /// Content sections within this toolbox talk
    /// </summary>
    public ICollection<ToolboxTalkSection> Sections { get; set; } = new List<ToolboxTalkSection>();

    /// <summary>
    /// Quiz questions for this toolbox talk
    /// </summary>
    public ICollection<ToolboxTalkQuestion> Questions { get; set; } = new List<ToolboxTalkQuestion>();

    /// <summary>
    /// Translations of this toolbox talk in different languages
    /// </summary>
    public ICollection<ToolboxTalkTranslation> Translations { get; set; } = new List<ToolboxTalkTranslation>();

    /// <summary>
    /// Video translations for this toolbox talk
    /// </summary>
    public ICollection<ToolboxTalkVideoTranslation> VideoTranslations { get; set; } = new List<ToolboxTalkVideoTranslation>();

    /// <summary>
    /// Slide images extracted from the PDF for slideshow display
    /// </summary>
    public virtual ICollection<ToolboxTalkSlide> Slides { get; set; } = new List<ToolboxTalkSlide>();

    /// <summary>
    /// Translated HTML slideshows for different languages
    /// </summary>
    public ICollection<ToolboxTalkSlideshowTranslation> SlideshowTranslations { get; set; } = new List<ToolboxTalkSlideshowTranslation>();
}
