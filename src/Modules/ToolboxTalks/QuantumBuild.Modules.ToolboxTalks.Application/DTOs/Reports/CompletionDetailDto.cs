namespace QuantumBuild.Modules.ToolboxTalks.Application.DTOs.Reports;

/// <summary>
/// Detailed completion record for reporting
/// </summary>
public record CompletionDetailDto
{
    /// <summary>
    /// Scheduled talk ID
    /// </summary>
    public Guid ScheduledTalkId { get; init; }

    /// <summary>
    /// Completion record ID
    /// </summary>
    public Guid CompletionId { get; init; }

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
    /// When the talk was assigned
    /// </summary>
    public DateTime RequiredDate { get; init; }

    /// <summary>
    /// When the talk was due
    /// </summary>
    public DateTime DueDate { get; init; }

    /// <summary>
    /// When the talk was completed
    /// </summary>
    public DateTime CompletedAt { get; init; }

    /// <summary>
    /// Time spent completing the talk in minutes
    /// </summary>
    public int TimeSpentMinutes { get; init; }

    /// <summary>
    /// Percentage of video watched
    /// </summary>
    public int? VideoWatchPercent { get; init; }

    /// <summary>
    /// Final quiz score achieved
    /// </summary>
    public int? QuizScore { get; init; }

    /// <summary>
    /// Maximum possible quiz score
    /// </summary>
    public int? QuizMaxScore { get; init; }

    /// <summary>
    /// Whether the quiz was passed
    /// </summary>
    public bool? QuizPassed { get; init; }

    /// <summary>
    /// Quiz score as percentage
    /// </summary>
    public decimal? QuizScorePercentage { get; init; }

    /// <summary>
    /// Name entered when signing
    /// </summary>
    public string SignedByName { get; init; } = string.Empty;

    /// <summary>
    /// When the employee signed
    /// </summary>
    public DateTime SignedAt { get; init; }

    /// <summary>
    /// Whether the completion was on time (before due date)
    /// </summary>
    public bool CompletedOnTime { get; init; }

    /// <summary>
    /// URL to completion certificate (if generated)
    /// </summary>
    public string? CertificateUrl { get; init; }

    // Start geolocation
    public double? StartedLatitude { get; init; }
    public double? StartedLongitude { get; init; }
    public double? StartedAccuracyMeters { get; init; }
    public DateTime? StartedLocationTimestamp { get; init; }

    // Completion geolocation
    public double? CompletedLatitude { get; init; }
    public double? CompletedLongitude { get; init; }
    public double? CompletedAccuracyMeters { get; init; }
    public DateTime? CompletedLocationTimestamp { get; init; }
}
