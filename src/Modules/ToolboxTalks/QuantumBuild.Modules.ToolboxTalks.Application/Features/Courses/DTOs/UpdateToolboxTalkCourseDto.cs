using System.ComponentModel.DataAnnotations;

namespace QuantumBuild.Modules.ToolboxTalks.Application.Features.Courses.DTOs;

/// <summary>
/// Request DTO for updating a toolbox talk course
/// </summary>
public record UpdateToolboxTalkCourseDto
{
    [Required]
    [MaxLength(200)]
    public string Title { get; init; } = string.Empty;

    [MaxLength(2000)]
    public string? Description { get; init; }

    public bool IsActive { get; init; }
    public bool RequireSequentialCompletion { get; init; }
    public bool RequiresRefresher { get; init; }
    public int RefresherIntervalMonths { get; init; }
    public bool GenerateCertificate { get; init; }
    public bool AutoAssignToNewEmployees { get; init; }
    public int AutoAssignDueDays { get; init; }
}
