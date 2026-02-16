using MediatR;
using QuantumBuild.Modules.ToolboxTalks.Application.Features.Courses.DTOs;

namespace QuantumBuild.Modules.ToolboxTalks.Application.Features.Courses.Commands;

/// <summary>
/// Command to update an existing toolbox talk course
/// </summary>
public record UpdateToolboxTalkCourseCommand : IRequest<ToolboxTalkCourseDto>
{
    public Guid Id { get; init; }
    public Guid TenantId { get; init; }
    public UpdateToolboxTalkCourseDto Dto { get; init; } = null!;
}
