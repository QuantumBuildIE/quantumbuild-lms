using MediatR;
using QuantumBuild.Modules.ToolboxTalks.Application.Features.CourseAssignments.DTOs;

namespace QuantumBuild.Modules.ToolboxTalks.Application.Features.CourseAssignments.Queries;

public record GetCourseAssignmentsQuery : IRequest<List<CourseAssignmentListDto>>
{
    public Guid TenantId { get; init; }
    public Guid CourseId { get; init; }
}
