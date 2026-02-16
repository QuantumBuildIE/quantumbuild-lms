using System.ComponentModel.DataAnnotations;
using QuantumBuild.Core.Domain.Common;

namespace QuantumBuild.Modules.ToolboxTalks.Domain.Entities;

/// <summary>
/// Records the completion of a scheduled toolbox talk by an employee.
/// Includes signature data, quiz results, and tracking information.
/// </summary>
public class ScheduledTalkCompletion : BaseEntity
{
    /// <summary>
    /// The scheduled talk that was completed (unique - one completion per scheduled talk)
    /// </summary>
    public Guid ScheduledTalkId { get; set; }

    /// <summary>
    /// When the talk was completed
    /// </summary>
    public DateTime CompletedAt { get; set; }

    /// <summary>
    /// Total time spent completing the talk in seconds
    /// </summary>
    public int TotalTimeSpentSeconds { get; set; }

    /// <summary>
    /// Percentage of video watched (0-100), null if no video
    /// </summary>
    public int? VideoWatchPercent { get; set; }

    /// <summary>
    /// Final quiz score achieved, null if no quiz
    /// </summary>
    public int? QuizScore { get; set; }

    /// <summary>
    /// Maximum possible quiz score, null if no quiz
    /// </summary>
    public int? QuizMaxScore { get; set; }

    /// <summary>
    /// Whether the quiz was passed, null if no quiz
    /// </summary>
    public bool? QuizPassed { get; set; }

    /// <summary>
    /// Base64 encoded signature image captured at completion
    /// </summary>
    public string SignatureData { get; set; } = string.Empty;

    /// <summary>
    /// When the employee signed
    /// </summary>
    public DateTime SignedAt { get; set; }

    /// <summary>
    /// Name entered by the employee when signing
    /// </summary>
    [MaxLength(200)]
    public string SignedByName { get; set; } = string.Empty;

    /// <summary>
    /// IP address of the device used to complete the talk
    /// </summary>
    [MaxLength(50)]
    public string? IPAddress { get; set; }

    /// <summary>
    /// User agent string of the browser/device used
    /// </summary>
    [MaxLength(500)]
    public string? UserAgent { get; set; }

    /// <summary>
    /// URL to the generated completion certificate PDF
    /// </summary>
    [MaxLength(500)]
    public string? CertificateUrl { get; set; }

    // Geolocation when completed
    public double? CompletedLatitude { get; set; }
    public double? CompletedLongitude { get; set; }
    public double? CompletedAccuracyMeters { get; set; }
    public DateTime? CompletedLocationTimestamp { get; set; }

    // Navigation properties

    /// <summary>
    /// The parent scheduled talk
    /// </summary>
    public ScheduledTalk ScheduledTalk { get; set; } = null!;
}
