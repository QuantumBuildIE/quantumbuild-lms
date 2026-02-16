using MediatR;
using QuantumBuild.Modules.ToolboxTalks.Application.DTOs;

namespace QuantumBuild.Modules.ToolboxTalks.Application.Queries.GetToolboxTalkScheduleById;

/// <summary>
/// Query to retrieve a single toolbox talk schedule by ID with full details
/// </summary>
public record GetToolboxTalkScheduleByIdQuery : IRequest<ToolboxTalkScheduleDto?>
{
    /// <summary>
    /// Tenant ID for multi-tenancy filtering
    /// </summary>
    public Guid TenantId { get; init; }

    /// <summary>
    /// The ID of the schedule to retrieve
    /// </summary>
    public Guid Id { get; init; }
}
