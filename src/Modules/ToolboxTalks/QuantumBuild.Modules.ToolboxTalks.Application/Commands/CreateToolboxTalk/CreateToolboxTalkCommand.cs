using MediatR;
using QuantumBuild.Modules.ToolboxTalks.Application.DTOs;
using QuantumBuild.Modules.ToolboxTalks.Domain.Enums;

namespace QuantumBuild.Modules.ToolboxTalks.Application.Commands.CreateToolboxTalk;

/// <summary>
/// Command to create a new toolbox talk
/// </summary>
public record CreateToolboxTalkCommand : IRequest<ToolboxTalkDto>
{
    public Guid TenantId { get; init; }
    public string Title { get; init; } = string.Empty;
    public string? Description { get; init; }
    public string? Category { get; init; }
    public ToolboxTalkFrequency Frequency { get; init; } = ToolboxTalkFrequency.Once;
    public string? VideoUrl { get; init; }
    public VideoSource VideoSource { get; init; } = VideoSource.None;
    public string? AttachmentUrl { get; init; }
    public int MinimumVideoWatchPercent { get; init; } = 90;
    public bool RequiresQuiz { get; init; } = false;
    public int? PassingScore { get; init; } = 80;
    public bool IsActive { get; init; } = true;

    // Quiz randomization settings
    public int? QuizQuestionCount { get; init; }
    public bool ShuffleQuestions { get; init; } = false;
    public bool ShuffleOptions { get; init; } = false;
    public bool UseQuestionPool { get; init; } = false;

    // Auto-assignment settings
    public bool AutoAssignToNewEmployees { get; init; } = false;
    public int AutoAssignDueDays { get; init; } = 14;

    // Source language
    public string SourceLanguageCode { get; init; } = "en";

    // Slideshow settings
    public bool GenerateSlidesFromPdf { get; init; } = false;

    /// <summary>
    /// Content sections for this toolbox talk
    /// </summary>
    public List<CreateToolboxTalkSectionDto> Sections { get; init; } = new();

    /// <summary>
    /// Quiz questions for this toolbox talk
    /// </summary>
    public List<CreateToolboxTalkQuestionDto> Questions { get; init; } = new();
}
