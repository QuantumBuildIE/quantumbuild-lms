namespace QuantumBuild.Modules.ToolboxTalks.Application.Features.Courses.DTOs;

/// <summary>
/// Request DTO for reordering/bulk updating course items
/// </summary>
public record UpdateCourseItemsDto
{
    public List<CourseItemOrderDto> Items { get; init; } = new();
}

/// <summary>
/// DTO for a single course item with its order and required status
/// </summary>
public record CourseItemOrderDto
{
    public Guid ToolboxTalkId { get; init; }
    public int OrderIndex { get; init; }
    public bool IsRequired { get; init; } = true;
}
