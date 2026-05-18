namespace QuantumBuild.Modules.ToolboxTalks.Application.DTOs;

public record TenantUsageRowDto
{
    public Guid TenantId { get; init; }
    public string TenantName { get; init; } = string.Empty;
    public DateTime SignUpDate { get; init; }
    public int ActiveEmployeeCount { get; init; }
    public int TotalLearnings { get; init; }
    public int NewLearnings { get; init; }
    public int Completions { get; init; }
    public DateTimeOffset? LastLoginAt { get; init; }
    public bool IsAtRisk { get; init; }
}

public record CustomerUsageReportDto
{
    public DateTimeOffset? LastReviewedAt { get; init; }
    public DateTimeOffset ComparisonDate { get; init; }
    public IReadOnlyList<TenantUsageRowDto> Rows { get; init; } = [];
}
