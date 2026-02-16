namespace QuantumBuild.Modules.ToolboxTalks.Application.DTOs.Reports;

/// <summary>
/// Compliance report showing overall toolbox talk completion metrics
/// </summary>
public record ComplianceReportDto
{
    /// <summary>
    /// Total number of employees in scope for the report
    /// </summary>
    public int TotalEmployees { get; init; }

    /// <summary>
    /// Number of employees with at least one toolbox talk assigned
    /// </summary>
    public int AssignedCount { get; init; }

    /// <summary>
    /// Number of completed toolbox talk assignments
    /// </summary>
    public int CompletedCount { get; init; }

    /// <summary>
    /// Overall compliance percentage (CompletedCount / AssignedCount * 100)
    /// </summary>
    public decimal CompliancePercentage { get; init; }

    /// <summary>
    /// Number of overdue assignments
    /// </summary>
    public int OverdueCount { get; init; }

    /// <summary>
    /// Number of assignments pending (not started)
    /// </summary>
    public int PendingCount { get; init; }

    /// <summary>
    /// Number of assignments in progress
    /// </summary>
    public int InProgressCount { get; init; }

    /// <summary>
    /// Compliance breakdown by department
    /// </summary>
    public List<DepartmentComplianceDto> ByDepartment { get; init; } = new();

    /// <summary>
    /// Compliance breakdown by individual toolbox talk
    /// </summary>
    public List<TalkComplianceDto> ByTalk { get; init; } = new();

    /// <summary>
    /// Date range start for the report
    /// </summary>
    public DateTime? DateFrom { get; init; }

    /// <summary>
    /// Date range end for the report
    /// </summary>
    public DateTime? DateTo { get; init; }

    /// <summary>
    /// When the report was generated
    /// </summary>
    public DateTime GeneratedAt { get; init; }
}

/// <summary>
/// Department-level compliance breakdown
/// </summary>
public record DepartmentComplianceDto
{
    /// <summary>
    /// Department/Site ID
    /// </summary>
    public Guid? SiteId { get; init; }

    /// <summary>
    /// Department/Site name
    /// </summary>
    public string DepartmentName { get; init; } = string.Empty;

    /// <summary>
    /// Total employees in department
    /// </summary>
    public int TotalEmployees { get; init; }

    /// <summary>
    /// Number of assigned talks
    /// </summary>
    public int AssignedCount { get; init; }

    /// <summary>
    /// Number of completed talks
    /// </summary>
    public int CompletedCount { get; init; }

    /// <summary>
    /// Department compliance percentage
    /// </summary>
    public decimal CompliancePercentage { get; init; }

    /// <summary>
    /// Number of overdue assignments in department
    /// </summary>
    public int OverdueCount { get; init; }
}

/// <summary>
/// Individual toolbox talk compliance breakdown
/// </summary>
public record TalkComplianceDto
{
    /// <summary>
    /// Toolbox talk ID
    /// </summary>
    public Guid ToolboxTalkId { get; init; }

    /// <summary>
    /// Toolbox talk title
    /// </summary>
    public string Title { get; init; } = string.Empty;

    /// <summary>
    /// Number of employees assigned this talk
    /// </summary>
    public int AssignedCount { get; init; }

    /// <summary>
    /// Number who completed this talk
    /// </summary>
    public int CompletedCount { get; init; }

    /// <summary>
    /// Compliance percentage for this talk
    /// </summary>
    public decimal CompliancePercentage { get; init; }

    /// <summary>
    /// Number of overdue assignments for this talk
    /// </summary>
    public int OverdueCount { get; init; }

    /// <summary>
    /// Average quiz score for this talk (if applicable)
    /// </summary>
    public decimal? AverageQuizScore { get; init; }

    /// <summary>
    /// Quiz pass rate for this talk (if applicable)
    /// </summary>
    public decimal? QuizPassRate { get; init; }
}
