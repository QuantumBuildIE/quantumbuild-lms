using MediatR;
using QuantumBuild.Modules.ToolboxTalks.Application.Features.CourseAssignments.DTOs;

namespace QuantumBuild.Modules.ToolboxTalks.Application.Features.CourseAssignments.Queries;

public record GetEmployeeCourseAssignmentsQuery : IRequest<List<ToolboxTalkCourseAssignmentDto>>
{
    public Guid TenantId { get; init; }
    public Guid EmployeeId { get; init; }
}
