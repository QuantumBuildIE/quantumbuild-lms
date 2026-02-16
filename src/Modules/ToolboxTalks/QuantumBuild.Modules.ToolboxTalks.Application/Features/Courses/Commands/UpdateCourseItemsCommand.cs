using MediatR;
using QuantumBuild.Modules.ToolboxTalks.Application.Features.Courses.DTOs;

namespace QuantumBuild.Modules.ToolboxTalks.Application.Features.Courses.Commands;

/// <summary>
/// Command to reorder/bulk update course items
/// </summary>
public record UpdateCourseItemsCommand : IRequest<ToolboxTalkCourseDto>
{
    public Guid CourseId { get; init; }
    public Guid TenantId { get; init; }
    public UpdateCourseItemsDto Dto { get; init; } = null!;
}
