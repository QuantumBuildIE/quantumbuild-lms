namespace QuantumBuild.Modules.ToolboxTalks.Application.DTOs.Reports;

/// <summary>
/// Skills matrix report showing employees × learnings with completion status
/// </summary>
public record SkillsMatrixDto
{
    public List<SkillsMatrixEmployeeDto> Employees { get; init; } = new();
    public List<SkillsMatrixLearningDto> Learnings { get; init; } = new();
    public List<SkillsMatrixCellDto> Cells { get; init; } = new();
}

/// <summary>
/// Employee row in the skills matrix
/// </summary>
public record SkillsMatrixEmployeeDto
{
    public Guid Id { get; init; }
    public string EmployeeCode { get; init; } = string.Empty;
    public string FullName { get; init; } = string.Empty;
    public string? Department { get; init; }
    public string? JobTitle { get; init; }
}

/// <summary>
/// Learning column in the skills matrix
/// </summary>
public record SkillsMatrixLearningDto
{
    public Guid Id { get; init; }
    public string Code { get; init; } = string.Empty;
    public string Title { get; init; } = string.Empty;
    public string? Category { get; init; }
}

/// <summary>
/// Individual cell in the skills matrix (employee × learning intersection)
/// </summary>
public record SkillsMatrixCellDto
{
    public Guid EmployeeId { get; init; }
    public Guid LearningId { get; init; }
    public string Status { get; init; } = "NotAssigned";
    public int? Score { get; init; }
    public DateTime? CompletedAt { get; init; }
    public DateTime? DueDate { get; init; }
    public int? DaysOverdue { get; init; }
}
