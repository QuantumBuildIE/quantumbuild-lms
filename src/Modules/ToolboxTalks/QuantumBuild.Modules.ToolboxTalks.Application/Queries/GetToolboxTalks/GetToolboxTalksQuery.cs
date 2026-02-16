using MediatR;
using QuantumBuild.Core.Application.Models;
using QuantumBuild.Modules.ToolboxTalks.Application.DTOs;
using QuantumBuild.Modules.ToolboxTalks.Domain.Enums;

namespace QuantumBuild.Modules.ToolboxTalks.Application.Queries.GetToolboxTalks;

/// <summary>
/// Query to retrieve a paginated list of toolbox talks with filtering options
/// </summary>
public record GetToolboxTalksQuery : IRequest<PaginatedList<ToolboxTalkListDto>>
{
    /// <summary>
    /// Tenant ID for multi-tenancy filtering
    /// </summary>
    public Guid TenantId { get; init; }

    /// <summary>
    /// Optional search term to filter by title or description
    /// </summary>
    public string? SearchTerm { get; init; }

    /// <summary>
    /// Optional filter by frequency
    /// </summary>
    public ToolboxTalkFrequency? Frequency { get; init; }

    /// <summary>
    /// Optional filter by active status
    /// </summary>
    public bool? IsActive { get; init; }

    /// <summary>
    /// Page number (1-based)
    /// </summary>
    public int PageNumber { get; init; } = 1;

    /// <summary>
    /// Number of items per page
    /// </summary>
    public int PageSize { get; init; } = 10;
}
