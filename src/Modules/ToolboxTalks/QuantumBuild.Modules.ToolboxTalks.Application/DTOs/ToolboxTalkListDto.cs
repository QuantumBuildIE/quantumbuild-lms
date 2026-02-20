using QuantumBuild.Modules.ToolboxTalks.Domain.Enums;

namespace QuantumBuild.Modules.ToolboxTalks.Application.DTOs;

/// <summary>
/// Lightweight DTO for toolbox talk lists
/// </summary>
public record ToolboxTalkListDto
{
    public Guid Id { get; init; }
    public string Code { get; init; } = string.Empty;
    public string Title { get; init; } = string.Empty;
    public string? Description { get; init; }
    public string? Category { get; init; }
    public ToolboxTalkFrequency Frequency { get; init; }
    public string FrequencyDisplay { get; init; } = string.Empty;
    public bool IsActive { get; init; }
    public bool HasVideo { get; init; }
    public bool RequiresQuiz { get; init; }
    public ToolboxTalkStatus Status { get; init; }
    public string StatusDisplay { get; init; } = string.Empty;
    public bool GeneratedFromVideo { get; init; }
    public bool GeneratedFromPdf { get; init; }

    // Auto-assignment
    public bool AutoAssignToNewEmployees { get; init; }

    // Counts
    public int SectionCount { get; init; }
    public int QuestionCount { get; init; }

    // Completion stats
    public ToolboxTalkCompletionStatsDto? CompletionStats { get; init; }

    // Audit
    public DateTime CreatedAt { get; init; }
}
