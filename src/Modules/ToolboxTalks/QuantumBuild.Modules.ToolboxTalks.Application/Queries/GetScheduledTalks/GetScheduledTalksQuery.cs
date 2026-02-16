using MediatR;
using QuantumBuild.Core.Application.Models;
using QuantumBuild.Modules.ToolboxTalks.Application.DTOs;
using QuantumBuild.Modules.ToolboxTalks.Domain.Enums;

namespace QuantumBuild.Modules.ToolboxTalks.Application.Queries.GetScheduledTalks;

/// <summary>
/// Query to retrieve a paginated list of scheduled talks (admin view of all assignments)
/// </summary>
public record GetScheduledTalksQuery : IRequest<PaginatedList<ScheduledTalkListDto>>
{
    /// <summary>
    /// Tenant ID for multi-tenancy filtering
    /// </summary>
    public Guid TenantId { get; init; }

    /// <summary>
    /// Optional filter by employee ID
    /// </summary>
    public Guid? EmployeeId { get; init; }

    /// <summary>
    /// Optional filter by toolbox talk ID
    /// </summary>
    public Guid? ToolboxTalkId { get; init; }

    /// <summary>
    /// Optional filter by assignment status
    /// </summary>
    public ScheduledTalkStatus? Status { get; init; }

    /// <summary>
    /// Optional filter for due dates from this date
    /// </summary>
    public DateTime? DueDateFrom { get; init; }

    /// <summary>
    /// Optional filter for due dates until this date
    /// </summary>
    public DateTime? DueDateTo { get; init; }

    /// <summary>
    /// Page number (1-based)
    /// </summary>
    public int PageNumber { get; init; } = 1;

    /// <summary>
    /// Number of items per page
    /// </summary>
    public int PageSize { get; init; } = 10;
}
