using MediatR;
using QuantumBuild.Modules.ToolboxTalks.Application.DTOs;

namespace QuantumBuild.Modules.ToolboxTalks.Application.Queries.GetMyToolboxTalkById;

/// <summary>
/// Query to retrieve a single scheduled talk for the current employee with full content
/// Includes translation support based on employee's preferred language
/// </summary>
public record GetMyToolboxTalkByIdQuery : IRequest<MyToolboxTalkDto?>
{
    /// <summary>
    /// Tenant ID for multi-tenancy filtering
    /// </summary>
    public Guid TenantId { get; init; }

    /// <summary>
    /// Current user's employee ID (resolved from context)
    /// </summary>
    public Guid EmployeeId { get; init; }

    /// <summary>
    /// The ID of the scheduled talk to retrieve
    /// </summary>
    public Guid ScheduledTalkId { get; init; }
}
