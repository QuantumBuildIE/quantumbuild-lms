using System.ComponentModel.DataAnnotations;

namespace QuantumBuild.Modules.ToolboxTalks.Application.Features.CourseAssignments.DTOs;

public record AssignCourseDto
{
    [Required]
    public Guid CourseId { get; init; }

    /// <summary>
    /// Explicit per-employee assignments (with optional per-employee talk selection).
    /// May be empty if TargetDepartmentIds/TargetSiteIds are supplied instead.
    /// </summary>
    public List<EmployeeCourseAssignmentDto> Assignments { get; init; } = new();

    /// <summary>
    /// Department IDs to target — expands to member employees, unioned with Assignments/TargetSiteIds.
    /// Expanded employees receive all course talks (no per-employee IncludedTalkIds).
    /// </summary>
    public List<Guid> TargetDepartmentIds { get; init; } = new();

    /// <summary>
    /// Site/Location IDs to target — expands to member employees, unioned with Assignments/TargetDepartmentIds.
    /// Expanded employees receive all course talks (no per-employee IncludedTalkIds).
    /// </summary>
    public List<Guid> TargetSiteIds { get; init; } = new();

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
