using MediatR;
using QuantumBuild.Modules.ToolboxTalks.Application.Features.Courses.DTOs;

namespace QuantumBuild.Modules.ToolboxTalks.Application.Features.Courses.Queries;

/// <summary>
/// Query to retrieve a single course with items and translations
/// </summary>
public record GetToolboxTalkCourseByIdQuery : IRequest<ToolboxTalkCourseDto?>
{
    public Guid Id { get; init; }
    public Guid TenantId { get; init; }
}
