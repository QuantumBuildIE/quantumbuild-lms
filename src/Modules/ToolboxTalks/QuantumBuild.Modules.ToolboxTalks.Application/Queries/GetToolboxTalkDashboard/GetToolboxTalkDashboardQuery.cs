using MediatR;
using QuantumBuild.Modules.ToolboxTalks.Application.DTOs;

namespace QuantumBuild.Modules.ToolboxTalks.Application.Queries.GetToolboxTalkDashboard;

/// <summary>
/// Query to retrieve toolbox talk dashboard data with KPIs and analytics
/// </summary>
public record GetToolboxTalkDashboardQuery : IRequest<ToolboxTalkDashboardDto>
{
    /// <summary>
    /// Tenant ID for multi-tenancy filtering (resolved from context)
    /// </summary>
    public Guid TenantId { get; init; }
}
