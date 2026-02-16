using MediatR;
using QuantumBuild.Modules.ToolboxTalks.Application.Features.CourseAssignments.DTOs;

namespace QuantumBuild.Modules.ToolboxTalks.Application.Features.CourseAssignments.Commands;

public record AssignCourseCommand : IRequest<List<ToolboxTalkCourseAssignmentDto>>
{
    public Guid TenantId { get; init; }
    public AssignCourseDto Dto { get; init; } = null!;
}
