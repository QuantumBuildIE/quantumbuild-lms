using QuantumBuild.Modules.ToolboxTalks.Domain.Enums;

namespace QuantumBuild.Modules.ToolboxTalks.Application.DTOs.Validation;

/// <summary>
/// Individual category score within a regulatory assessment
/// </summary>
public record CategoryScoreDto
{
    public string Key { get; init; } = string.Empty;
    public string Label { get; init; } = string.Empty;
    public decimal Weight { get; init; }
    public int Score { get; init; }
}

/// <summary>
/// Request body for triggering a regulatory score assessment
/// </summary>
public record RegulatoryScoreRequestDto
{
    public Guid ValidationRunId { get; init; }
    public ValidationScoreType ScoreType { get; init; }
}

/// <summary>
/// Result of a single regulatory score assessment
/// </summary>
public record RegulatoryScoreResultDto
{
    public Guid Id { get; init; }
    public ValidationScoreType ScoreType { get; init; }
    public int OverallScore { get; init; }
    public string Verdict { get; init; } = string.Empty;
    public string Summary { get; init; } = string.Empty;
    public List<CategoryScoreDto> CategoryScores { get; init; } = new();
    public string RunLabel { get; init; } = string.Empty;
    public int RunNumber { get; init; }
    public string? RegulatoryBody { get; init; }
    /// <summary>
    /// From the RegulatoryProfile e.g. "HIQA Regulatory Score"
    /// </summary>
    public string? ScoreLabel { get; init; }
    public int ScoredSectionCount { get; init; }
    public string TargetLanguage { get; init; } = string.Empty;
    public DateTimeOffset CreatedAt { get; init; }
    /// <summary>
    /// Delta vs previous run of same type, null if first run
    /// </summary>
    public int? ComparisonDelta { get; init; }
    /// <summary>
    /// Full Claude response JSON for detailed per-category findings
    /// </summary>
    public string? FullResponse { get; init; }
}

/// <summary>
/// Full score history for a validation run across all three score types
/// </summary>
public record RegulatoryScoreHistoryDto
{
    public Guid ValidationRunId { get; init; }
    public RegulatoryScoreResultDto? SourceScore { get; init; }
    public RegulatoryScoreResultDto? PureScore { get; init; }
    /// <summary>
    /// Ordered by RunNumber
    /// </summary>
    public List<RegulatoryScoreResultDto> RegulatoryScores { get; init; } = new();
}
