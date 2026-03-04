using QuantumBuild.Modules.ToolboxTalks.Domain.Enums;

namespace QuantumBuild.Modules.ToolboxTalks.Application.DTOs.Validation;

/// <summary>
/// Lightweight DTO for listing validation runs
/// </summary>
public record ValidationRunListDto
{
    public Guid Id { get; init; }
    public string LanguageCode { get; init; } = string.Empty;
    public string? SectorKey { get; init; }
    public int OverallScore { get; init; }
    public ValidationOutcome OverallOutcome { get; init; }
    public ValidationOutcome? SafetyVerdict { get; init; }
    public ValidationRunStatus Status { get; init; }
    public int TotalSections { get; init; }
    public int PassedSections { get; init; }
    public int ReviewSections { get; init; }
    public int FailedSections { get; init; }
    public int PassThreshold { get; init; }
    public string? AuditReportUrl { get; init; }
    public DateTime? StartedAt { get; init; }
    public DateTime? CompletedAt { get; init; }
    public DateTime CreatedAt { get; init; }
}
