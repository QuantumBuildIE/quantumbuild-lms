using MediatR;
using QuantumBuild.Modules.ToolboxTalks.Application.Features.Courses.DTOs;

namespace QuantumBuild.Modules.ToolboxTalks.Application.Features.Courses.Queries;

/// <summary>
/// Query to retrieve all courses for a tenant
/// </summary>
public record GetToolboxTalkCoursesQuery : IRequest<List<ToolboxTalkCourseListDto>>
{
    public Guid TenantId { get; init; }
    public string? SearchTerm { get; init; }
    public bool? IsActive { get; init; }
}
