using MediatR;
using QuantumBuild.Core.Application.Models;
using QuantumBuild.Modules.ToolboxTalks.Application.DTOs;

namespace QuantumBuild.Modules.ToolboxTalks.Application.Commands.UpdateToolboxTalkSettings;

/// <summary>
/// Wizard Step 4 — update talk settings (title, description, category, behaviour toggles, refresher).
/// Title/Description changes automatically mark existing translations stale via MarkStale.
/// All other field changes (category, toggles, refresher) do not affect translations.
/// </summary>
public record UpdateToolboxTalkSettingsCommand(
    Guid TalkId,
    Guid TenantId,
    string Title,
    string? Description,
    string? Category,
    RefresherFrequency RefresherFrequency,
    bool IsActive,
    bool GenerateCertificate,
    int MinimumVideoWatchPercent,
    bool AutoAssignToNewEmployees,
    int AutoAssignDueDays,
    bool GenerateSlidesFromPdf
) : IRequest<Result<ToolboxTalkDto>>;

/// <summary>
/// Wizard-facing enum mapping to RequiresRefresher + RefresherIntervalMonths.
/// </summary>
public enum RefresherFrequency
{
    Once,
    Monthly,
    Quarterly,
    Annually
}
