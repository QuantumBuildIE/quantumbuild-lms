using System.ComponentModel.DataAnnotations;

namespace QuantumBuild.Modules.ToolboxTalks.Application.Features.Courses.DTOs;

/// <summary>
/// Request DTO for creating a new toolbox talk course
/// </summary>
public record CreateToolboxTalkCourseDto
{
    [Required]
    [MaxLength(200)]
    public string Title { get; init; } = string.Empty;

    [MaxLength(2000)]
    public string? Description { get; init; }

    public bool IsActive { get; init; } = true;
    public bool RequireSequentialCompletion { get; init; } = true;
    public bool RequiresRefresher { get; init; } = false;
    public int RefresherIntervalMonths { get; init; } = 12;
    public bool GenerateCertificate { get; init; } = false;
    public bool AutoAssignToNewEmployees { get; init; } = false;
    public int AutoAssignDueDays { get; init; } = 14;

    /// <summary>
    /// Optional: add talks during creation
    /// </summary>
    public List<CreateToolboxTalkCourseItemDto>? Items { get; init; }
}
