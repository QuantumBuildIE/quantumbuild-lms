using MediatR;
using QuantumBuild.Core.Application.Models;
using QuantumBuild.Modules.ToolboxTalks.Application.DTOs;
using QuantumBuild.Modules.ToolboxTalks.Domain.Enums;

namespace QuantumBuild.Modules.ToolboxTalks.Application.Queries.GetMyToolboxTalks;

/// <summary>
/// Query to retrieve the current employee's assigned toolbox talks (employee portal)
/// </summary>
public record GetMyToolboxTalksQuery : IRequest<PaginatedList<MyToolboxTalkListDto>>
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
    /// Optional filter by assignment status
    /// </summary>
    public ScheduledTalkStatus? Status { get; init; }

    /// <summary>
    /// Page number (1-based)
    /// </summary>
    public int PageNumber { get; init; } = 1;

    /// <summary>
    /// Number of items per page
    /// </summary>
    public int PageSize { get; init; } = 10;
}
