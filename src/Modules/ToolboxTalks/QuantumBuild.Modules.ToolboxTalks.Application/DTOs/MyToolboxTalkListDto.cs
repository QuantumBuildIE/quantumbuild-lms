using QuantumBuild.Modules.ToolboxTalks.Domain.Enums;

namespace QuantumBuild.Modules.ToolboxTalks.Application.DTOs;

/// <summary>
/// Lightweight DTO for employee portal toolbox talk list
/// </summary>
public record MyToolboxTalkListDto
{
    public Guid ScheduledTalkId { get; init; }
    public Guid ToolboxTalkId { get; init; }
    public string Title { get; init; } = string.Empty;
    public string? Description { get; init; }
    public DateTime RequiredDate { get; init; }
    public DateTime DueDate { get; init; }
    public ScheduledTalkStatus Status { get; init; }
    public string StatusDisplay { get; init; } = string.Empty;

    // Features
    public bool HasVideo { get; init; }
    public bool RequiresQuiz { get; init; }

    // Progress
    public int TotalSections { get; init; }
    public int CompletedSections { get; init; }
    public decimal ProgressPercent { get; init; }

    // Timing
    public bool IsOverdue { get; init; }
    public int DaysUntilDue { get; init; }

    // Refresher
    public bool IsRefresher { get; init; }
    public DateTime? RefresherDueDate { get; init; }
}
