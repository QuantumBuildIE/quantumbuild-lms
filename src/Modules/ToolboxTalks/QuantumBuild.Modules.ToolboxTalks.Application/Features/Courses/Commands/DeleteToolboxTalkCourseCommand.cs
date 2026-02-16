using MediatR;

namespace QuantumBuild.Modules.ToolboxTalks.Application.Features.Courses.Commands;

/// <summary>
/// Command to soft-delete a toolbox talk course
/// </summary>
public record DeleteToolboxTalkCourseCommand : IRequest<bool>
{
    public Guid Id { get; init; }
    public Guid TenantId { get; init; }
}
