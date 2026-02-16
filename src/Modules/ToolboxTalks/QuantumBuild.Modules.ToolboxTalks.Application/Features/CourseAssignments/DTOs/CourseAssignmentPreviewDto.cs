namespace QuantumBuild.Modules.ToolboxTalks.Application.Features.CourseAssignments.DTOs;

public record CourseAssignmentPreviewDto
{
    public Guid CourseId { get; init; }
    public string CourseTitle { get; init; } = string.Empty;
    public List<CourseAssignmentEmployeePreviewDto> Employees { get; init; } = new();
}

public record CourseAssignmentEmployeePreviewDto
{
    public Guid EmployeeId { get; init; }
    public string EmployeeName { get; init; } = string.Empty;
    public string? EmployeeCode { get; init; }
    public List<CourseTalkPreviewDto> Talks { get; init; } = new();
    public int CompletedCount { get; init; }
    public int TotalCount { get; init; }
}

public record CourseTalkPreviewDto
{
    public Guid ToolboxTalkId { get; init; }
    public string Title { get; init; } = string.Empty;
    public int OrderIndex { get; init; }
    public bool IsRequired { get; init; }
    public bool AlreadyCompleted { get; init; }
    public DateTime? CompletedAt { get; init; }
}
