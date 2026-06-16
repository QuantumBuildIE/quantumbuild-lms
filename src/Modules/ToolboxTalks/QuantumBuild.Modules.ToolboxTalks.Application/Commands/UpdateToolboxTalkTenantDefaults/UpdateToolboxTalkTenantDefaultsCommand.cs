using MediatR;
using QuantumBuild.Core.Application.Models;
using QuantumBuild.Modules.ToolboxTalks.Application.DTOs;

namespace QuantumBuild.Modules.ToolboxTalks.Application.Commands.UpdateToolboxTalkTenantDefaults;

/// <summary>
/// Updates the five wizard Step 4 default fields on the tenant's ToolboxTalkSettings row.
/// Creates the row if none exists. Distinct from UpdateToolboxTalkSettingsCommand, which
/// updates per-talk settings for a specific talk in wizard Step 4.
/// </summary>
public record UpdateToolboxTalkTenantDefaultsCommand(
    Guid TenantId,
    int DefaultMinimumVideoWatchPercent,
    int DefaultAutoAssignDueDays,
    bool DefaultGenerateCertificate,
    string DefaultRefresherFrequency,
    bool DefaultIsActive
) : IRequest<Result<ToolboxTalkSettingsDto>>;
