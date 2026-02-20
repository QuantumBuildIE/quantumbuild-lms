using QuantumBuild.Modules.ToolboxTalks.Domain.Enums;

namespace QuantumBuild.Modules.ToolboxTalks.Application.DTOs;

/// <summary>
/// DTO for an individual scheduled talk assignment to an employee
/// </summary>
public record ScheduledTalkDto
{
    public Guid Id { get; init; }
    public Guid ToolboxTalkId { get; init; }
    public string ToolboxTalkCode { get; init; } = string.Empty;
    public string ToolboxTalkTitle { get; init; } = string.Empty;
    public Guid EmployeeId { get; init; }
    public string EmployeeName { get; init; } = string.Empty;
    public string? EmployeeEmail { get; init; }
    public Guid? ScheduleId { get; init; }
    public DateTime RequiredDate { get; init; }
    public DateTime DueDate { get; init; }
    public ScheduledTalkStatus Status { get; init; }
    public string StatusDisplay { get; init; } = string.Empty;
    public int RemindersSent { get; init; }
    public DateTime? LastReminderAt { get; init; }
    public string LanguageCode { get; init; } = "en";

    // Progress
    public int TotalSections { get; init; }
    public int CompletedSections { get; init; }
    public decimal ProgressPercent { get; init; }

    // Child collections
    public List<ScheduledTalkSectionProgressDto> SectionProgress { get; init; } = new();
    public List<ScheduledTalkQuizAttemptDto> QuizAttempts { get; init; } = new();

    // Completion (if completed)
    public ScheduledTalkCompletionDto? Completion { get; init; }

    // Start geolocation
    public double? StartedLatitude { get; init; }
    public double? StartedLongitude { get; init; }
    public double? StartedAccuracyMeters { get; init; }
    public DateTime? StartedLocationTimestamp { get; init; }

    // Refresher
    public bool IsRefresher { get; init; }
    public Guid? OriginalScheduledTalkId { get; init; }
    public DateTime? RefresherDueDate { get; init; }

    // Audit
    public DateTime CreatedAt { get; init; }
    public DateTime? UpdatedAt { get; init; }
}
