using MediatR;
using QuantumBuild.Modules.ToolboxTalks.Application.Features.Courses.DTOs;

namespace QuantumBuild.Modules.ToolboxTalks.Application.Features.Courses.Commands;

/// <summary>
/// Command to create a new toolbox talk course
/// </summary>
public record CreateToolboxTalkCourseCommand : IRequest<ToolboxTalkCourseDto>
{
    public Guid TenantId { get; init; }
    public CreateToolboxTalkCourseDto Dto { get; init; } = null!;
}
