using QuantumBuild.Modules.ToolboxTalks.Domain.Enums;

namespace QuantumBuild.Modules.ToolboxTalks.Application.DTOs;

/// <summary>
/// Full DTO for a toolbox talk with all details including sections and questions
/// </summary>
public record ToolboxTalkDto
{
    public Guid Id { get; init; }
    public string Title { get; init; } = string.Empty;
    public string? Description { get; init; }
    public string? Category { get; init; }
    public ToolboxTalkFrequency Frequency { get; init; }
    public string FrequencyDisplay { get; init; } = string.Empty;
    public string? VideoUrl { get; init; }
    public VideoSource VideoSource { get; init; }
    public string VideoSourceDisplay { get; init; } = string.Empty;
    public string? AttachmentUrl { get; init; }
    public int MinimumVideoWatchPercent { get; init; }
    public bool RequiresQuiz { get; init; }
    public int? PassingScore { get; init; }
    public bool IsActive { get; init; }
    public ToolboxTalkStatus Status { get; init; }
    public string StatusDisplay { get; init; } = string.Empty;
    public string? PdfUrl { get; init; }
    public string? PdfFileName { get; init; }
    public bool GeneratedFromVideo { get; init; }
    public bool GeneratedFromPdf { get; init; }

    // Slideshow settings
    public bool GenerateSlidesFromPdf { get; init; }
    public bool SlidesGenerated { get; init; }
    public int SlideCount { get; init; }
    public string? SlideshowHtml { get; init; }
    public DateTime? SlideshowGeneratedAt { get; init; }
    public bool HasSlideshow => !string.IsNullOrEmpty(SlideshowHtml);

    // Quiz randomization settings
    public int? QuizQuestionCount { get; init; }
    public bool ShuffleQuestions { get; init; }
    public bool ShuffleOptions { get; init; }
    public bool UseQuestionPool { get; init; }

    // Source language
    public string SourceLanguageCode { get; init; } = "en";

    // Auto-assignment settings
    public bool AutoAssignToNewEmployees { get; init; }
    public int AutoAssignDueDays { get; init; }

    // Certificate settings
    public bool GenerateCertificate { get; init; }

    // Refresher settings
    public bool RequiresRefresher { get; init; }
    public int RefresherIntervalMonths { get; init; }

    // Child collections
    public List<ToolboxTalkSectionDto> Sections { get; init; } = new();
    public List<ToolboxTalkQuestionDto> Questions { get; init; } = new();
    public List<ToolboxTalkTranslationDto> Translations { get; init; } = new();

    // Completion stats (for list context)
    public ToolboxTalkCompletionStatsDto? CompletionStats { get; init; }

    // Audit
    public DateTime CreatedAt { get; init; }
    public DateTime? UpdatedAt { get; init; }
}

/// <summary>
/// Completion statistics for a toolbox talk
/// </summary>
public record ToolboxTalkCompletionStatsDto
{
    public int TotalAssignments { get; init; }
    public int CompletedCount { get; init; }
    public int OverdueCount { get; init; }
    public int PendingCount { get; init; }
    public int InProgressCount { get; init; }
    public decimal CompletionRate { get; init; }
}

/// <summary>
/// DTO for a toolbox talk content translation
/// </summary>
public record ToolboxTalkTranslationDto
{
    /// <summary>
    /// ISO 639-1 language code (e.g., "pl", "ro")
    /// </summary>
    public string LanguageCode { get; init; } = string.Empty;

    /// <summary>
    /// Display name of the language (e.g., "Polish", "Romanian")
    /// </summary>
    public string Language { get; init; } = string.Empty;

    /// <summary>
    /// Translated title
    /// </summary>
    public string TranslatedTitle { get; init; } = string.Empty;

    /// <summary>
    /// When the translation was created/updated
    /// </summary>
    public DateTime TranslatedAt { get; init; }

    /// <summary>
    /// Provider used for translation (e.g., "Claude", "Manual")
    /// </summary>
    public string TranslationProvider { get; init; } = string.Empty;
}
