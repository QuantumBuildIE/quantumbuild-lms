namespace QuantumBuild.Modules.ToolboxTalks.Application.DTOs.Reports;

/// <summary>
/// Represents an overdue toolbox talk assignment for reporting
/// </summary>
public record OverdueItemDto
{
    /// <summary>
    /// Scheduled talk ID
    /// </summary>
    public Guid ScheduledTalkId { get; init; }

    /// <summary>
    /// Employee ID
    /// </summary>
    public Guid EmployeeId { get; init; }

    /// <summary>
    /// Employee full name
    /// </summary>
    public string EmployeeName { get; init; } = string.Empty;

    /// <summary>
    /// Employee email address
    /// </summary>
    public string? Email { get; init; }

    /// <summary>
    /// Employee's assigned site/department
    /// </summary>
    public string? SiteName { get; init; }

    /// <summary>
    /// Toolbox talk ID
    /// </summary>
    public Guid ToolboxTalkId { get; init; }

    /// <summary>
    /// Toolbox talk code
    /// </summary>
    public string TalkCode { get; init; } = string.Empty;

    /// <summary>
    /// Toolbox talk title
    /// </summary>
    public string TalkTitle { get; init; } = string.Empty;

    /// <summary>
    /// Due date for the assignment
    /// </summary>
    public DateTime DueDate { get; init; }

    /// <summary>
    /// Number of days the assignment is overdue
    /// </summary>
    public int DaysOverdue { get; init; }

    /// <summary>
    /// Number of reminders sent to the employee
    /// </summary>
    public int RemindersSent { get; init; }

    /// <summary>
    /// Date of last reminder sent
    /// </summary>
    public DateTime? LastReminderAt { get; init; }

    /// <summary>
    /// Whether the employee has started but not completed
    /// </summary>
    public bool IsInProgress { get; init; }

    /// <summary>
    /// Video watch percentage if started
    /// </summary>
    public int VideoWatchPercent { get; init; }
}
