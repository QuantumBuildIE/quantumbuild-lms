using System.ComponentModel.DataAnnotations;

namespace QuantumBuild.Modules.ToolboxTalks.Application.Features.Courses.DTOs;

/// <summary>
/// Request DTO for adding a talk to a course
/// </summary>
public record CreateToolboxTalkCourseItemDto
{
    [Required]
    public Guid ToolboxTalkId { get; init; }

    public int OrderIndex { get; init; }
    public bool IsRequired { get; init; } = true;
}
