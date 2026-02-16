using System.ComponentModel.DataAnnotations;

namespace QuantumBuild.Modules.ToolboxTalks.Application.Features.CourseAssignments.DTOs;

public record AssignCourseDto
{
    [Required]
    public Guid CourseId { get; init; }

    [Required]
    [MinLength(1)]
    public List<EmployeeCourseAssignmentDto> Assignments { get; init; } = new();

    public DateTime? DueDate { get; init; }
}

public record EmployeeCourseAssignmentDto
{
    [Required]
    public Guid EmployeeId { get; init; }

    /// <summary>
    /// Talk IDs to include. If null or empty, all talks are included.
    /// </summary>
    public List<Guid>? IncludedTalkIds { get; init; }
}
