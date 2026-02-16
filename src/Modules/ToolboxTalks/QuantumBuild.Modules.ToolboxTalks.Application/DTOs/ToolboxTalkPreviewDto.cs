using QuantumBuild.Modules.ToolboxTalks.Domain.Enums;

namespace QuantumBuild.Modules.ToolboxTalks.Application.DTOs;

/// <summary>
/// Preview DTO showing a toolbox talk as an employee would see it,
/// with translated content applied for the specified language.
/// </summary>
public record ToolboxTalkPreviewDto
{
    public Guid Id { get; init; }
    public string Title { get; init; } = string.Empty;
    public string? Description { get; init; }
    public string? Category { get; init; }
    public string? VideoUrl { get; init; }
    public VideoSource VideoSource { get; init; }
    public bool RequiresQuiz { get; init; }
    public int? PassingScore { get; init; }
    public bool SlidesGenerated { get; init; }
    public int SlideCount { get; init; }
    public string? SlideshowHtml { get; init; }
    public DateTime? SlideshowGeneratedAt { get; init; }
    public bool HasSlideshow => !string.IsNullOrEmpty(SlideshowHtml);
    public string SourceLanguageCode { get; init; } = "en";
    public string PreviewLanguageCode { get; init; } = "en";

    /// <summary>
    /// Available translation language codes (plus the source language)
    /// </summary>
    public List<ToolboxTalkTranslationDto> AvailableTranslations { get; init; } = new();

    public List<PreviewSectionDto> Sections { get; init; } = new();
    public List<PreviewQuestionDto> Questions { get; init; } = new();
}

/// <summary>
/// Section as an employee would see it (translated content, no admin metadata)
/// </summary>
public record PreviewSectionDto
{
    public Guid Id { get; init; }
    public int SectionNumber { get; init; }
    public string Title { get; init; } = string.Empty;
    public string Content { get; init; } = string.Empty;
    public bool RequiresAcknowledgment { get; init; }
}

/// <summary>
/// Question as an employee would see it (no correct answer revealed)
/// </summary>
public record PreviewQuestionDto
{
    public Guid Id { get; init; }
    public int QuestionNumber { get; init; }
    public string QuestionText { get; init; } = string.Empty;
    public QuestionType QuestionType { get; init; }
    public string QuestionTypeDisplay { get; init; } = string.Empty;
    public List<string>? Options { get; init; }
    public int Points { get; init; }
}
