namespace QuantumBuild.Modules.ToolboxTalks.Application.DTOs;

/// <summary>
/// DTO for a completed scheduled talk record
/// </summary>
public record ScheduledTalkCompletionDto
{
    public Guid Id { get; init; }
    public Guid ScheduledTalkId { get; init; }
    public DateTime CompletedAt { get; init; }
    public int TotalTimeSpentSeconds { get; init; }
    public int? VideoWatchPercent { get; init; }
    public int? QuizScore { get; init; }
    public int? QuizMaxScore { get; init; }
    public bool? QuizPassed { get; init; }

    /// <summary>
    /// Base64 encoded signature image
    /// </summary>
    public string SignatureData { get; init; } = string.Empty;

    public DateTime SignedAt { get; init; }
    public string SignedByName { get; init; } = string.Empty;
    public string? IPAddress { get; init; }
    public string? UserAgent { get; init; }
    public string? CertificateUrl { get; init; }

    // Completion geolocation
    public double? CompletedLatitude { get; init; }
    public double? CompletedLongitude { get; init; }
    public double? CompletedAccuracyMeters { get; init; }
    public DateTime? CompletedLocationTimestamp { get; init; }
}
