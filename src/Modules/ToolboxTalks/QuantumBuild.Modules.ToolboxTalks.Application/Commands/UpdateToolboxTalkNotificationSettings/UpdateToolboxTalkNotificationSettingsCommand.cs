using MediatR;
using QuantumBuild.Core.Application.Models;
using QuantumBuild.Modules.ToolboxTalks.Application.DTOs;

namespace QuantumBuild.Modules.ToolboxTalks.Application.Commands.UpdateToolboxTalkNotificationSettings;

public record UpdateToolboxTalkNotificationSettingsCommand(
    Guid TenantId,
    bool NotifyOnTranslationComplete,
    bool NotifyOnValidationComplete,
    bool NotifyOnFailure,
    bool NotifyOnExternalReviewResponse
) : IRequest<Result<ToolboxTalkSettingsDto>>;
