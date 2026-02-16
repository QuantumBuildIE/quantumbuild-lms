using QuantumBuild.Modules.ToolboxTalks.Domain.Enums;

namespace QuantumBuild.Modules.ToolboxTalks.Application.DTOs;

/// <summary>
/// Dashboard DTO for toolbox talks overview and analytics
/// </summary>
public record ToolboxTalkDashboardDto
{
    // Talk counts
    public int TotalTalks { get; init; }
    public int ActiveTalks { get; init; }
    public int InactiveTalks { get; init; }

    // Assignment counts
    public int TotalAssignments { get; init; }
    public int PendingCount { get; init; }
    public int InProgressCount { get; init; }
    public int CompletedCount { get; init; }
    public int OverdueCount { get; init; }

    // Rates
    public decimal CompletionRate { get; init; }
    public decimal OverdueRate { get; init; }

    // Timing metrics
    public decimal AverageCompletionTimeHours { get; init; }
    public decimal AverageQuizScore { get; init; }
    public decimal QuizPassRate { get; init; }

    // Breakdowns
    public Dictionary<ScheduledTalkStatus, int> TalksByStatus { get; init; } = new();
    public Dictionary<ToolboxTalkFrequency, int> TalksByFrequency { get; init; } = new();

    // Recent activity
    public List<RecentCompletionDto> RecentCompletions { get; init; } = new();
    public List<OverdueAssignmentDto> OverdueAssignments { get; init; } = new();

    // Upcoming
    public List<UpcomingScheduleDto> UpcomingSchedules { get; init; } = new();
}

/// <summary>
/// Recent completion summary for dashboard
/// </summary>
public record RecentCompletionDto
{
    public Guid ScheduledTalkId { get; init; }
    public string EmployeeName { get; init; } = string.Empty;
    public string ToolboxTalkTitle { get; init; } = string.Empty;
    public DateTime CompletedAt { get; init; }
    public int TotalTimeSpentSeconds { get; init; }
    public bool? QuizPassed { get; init; }
    public decimal? QuizScore { get; init; }
}

/// <summary>
/// Overdue assignment summary for dashboard
/// </summary>
public record OverdueAssignmentDto
{
    public Guid ScheduledTalkId { get; init; }
    public Guid EmployeeId { get; init; }
    public string EmployeeName { get; init; } = string.Empty;
    public string? EmployeeEmail { get; init; }
    public string ToolboxTalkTitle { get; init; } = string.Empty;
    public DateTime DueDate { get; init; }
    public int DaysOverdue { get; init; }
    public int RemindersSent { get; init; }
    public ScheduledTalkStatus Status { get; init; }
}

/// <summary>
/// Upcoming schedule summary for dashboard
/// </summary>
public record UpcomingScheduleDto
{
    public Guid ScheduleId { get; init; }
    public string ToolboxTalkTitle { get; init; } = string.Empty;
    public DateTime ScheduledDate { get; init; }
    public ToolboxTalkFrequency Frequency { get; init; }
    public string FrequencyDisplay { get; init; } = string.Empty;
    public int AssignmentCount { get; init; }
    public bool AssignToAllEmployees { get; init; }
}
