using QuantumBuild.Modules.ToolboxTalks.Domain.Enums;

namespace QuantumBuild.Modules.ToolboxTalks.Application.DTOs.Validation;

/// <summary>
/// Detailed DTO for a single validation run with all its results
/// </summary>
public record ValidationRunDetailDto
{
    public Guid Id { get; init; }
    public Guid? ToolboxTalkId { get; init; }
    public Guid? CourseId { get; init; }
    public string LanguageCode { get; init; } = string.Empty;
    public string? SectorKey { get; init; }
    public string SourceLanguage { get; init; } = string.Empty;
    public string? SourceDialect { get; init; }
    public int PassThreshold { get; init; }
    public int OverallScore { get; init; }
    public ValidationOutcome OverallOutcome { get; init; }
    public ValidationOutcome? SafetyVerdict { get; init; }
    public int TotalSections { get; init; }
    public int PassedSections { get; init; }
    public int ReviewSections { get; init; }
    public int FailedSections { get; init; }
    public ValidationRunStatus Status { get; init; }
    public string? AuditReportUrl { get; init; }

    // Audit metadata
    public string? ReviewerName { get; init; }
    public string? ReviewerOrg { get; init; }
    public string? ReviewerRole { get; init; }
    public string? DocumentRef { get; init; }
    public string? ClientName { get; init; }
    public string? AuditPurpose { get; init; }

    // Pre-flight scan
    public string? PreFlightScanJson { get; init; }

    public DateTime? StartedAt { get; init; }
    public DateTime? CompletedAt { get; init; }
    public DateTime CreatedAt { get; init; }

    public List<ValidationResultDto> Results { get; init; } = new();
}
