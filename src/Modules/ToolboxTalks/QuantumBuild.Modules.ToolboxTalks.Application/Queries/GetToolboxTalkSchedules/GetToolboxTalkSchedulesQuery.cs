using MediatR;
using QuantumBuild.Core.Application.Models;
using QuantumBuild.Modules.ToolboxTalks.Application.DTOs;
using QuantumBuild.Modules.ToolboxTalks.Domain.Enums;

namespace QuantumBuild.Modules.ToolboxTalks.Application.Queries.GetToolboxTalkSchedules;

/// <summary>
/// Query to retrieve a paginated list of toolbox talk schedules
/// </summary>
public record GetToolboxTalkSchedulesQuery : IRequest<PaginatedList<ToolboxTalkScheduleListDto>>
{
    /// <summary>
    /// Tenant ID for multi-tenancy filtering
    /// </summary>
    public Guid TenantId { get; init; }

    /// <summary>
    /// Optional filter by toolbox talk ID
    /// </summary>
    public Guid? ToolboxTalkId { get; init; }

    /// <summary>
    /// Optional filter by schedule status
    /// </summary>
    public ToolboxTalkScheduleStatus? Status { get; init; }

    /// <summary>
    /// Optional filter for schedules from this date
    /// </summary>
    public DateTime? DateFrom { get; init; }

    /// <summary>
    /// Optional filter for schedules until this date
    /// </summary>
    public DateTime? DateTo { get; init; }

    /// <summary>
    /// Page number (1-based)
    /// </summary>
    public int PageNumber { get; init; } = 1;

    /// <summary>
    /// Number of items per page
    /// </summary>
    public int PageSize { get; init; } = 10;
}
