using MediatR;
using QuantumBuild.Modules.ToolboxTalks.Application.Features.Courses.DTOs;

namespace QuantumBuild.Modules.ToolboxTalks.Application.Features.Courses.Commands;

/// <summary>
/// Command to add a talk to a course
/// </summary>
public record AddCourseItemCommand : IRequest<ToolboxTalkCourseDto>
{
    public Guid CourseId { get; init; }
    public Guid TenantId { get; init; }
    public CreateToolboxTalkCourseItemDto Dto { get; init; } = null!;
}
