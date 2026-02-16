using MediatR;
using QuantumBuild.Modules.ToolboxTalks.Application.DTOs;

namespace QuantumBuild.Modules.ToolboxTalks.Application.Queries.GetToolboxTalkSettings;

/// <summary>
/// Query to retrieve toolbox talk settings for the current tenant
/// Returns defaults if no settings exist
/// </summary>
public record GetToolboxTalkSettingsQuery : IRequest<ToolboxTalkSettingsDto>
{
    /// <summary>
    /// Tenant ID for multi-tenancy filtering
    /// </summary>
    public Guid TenantId { get; init; }
}
