using MediatR;

namespace QuantumBuild.Modules.ToolboxTalks.Application.Features.CourseAssignments.Commands;

public record DeleteCourseAssignmentCommand : IRequest<bool>
{
    public Guid Id { get; init; }
    public Guid TenantId { get; init; }
}
