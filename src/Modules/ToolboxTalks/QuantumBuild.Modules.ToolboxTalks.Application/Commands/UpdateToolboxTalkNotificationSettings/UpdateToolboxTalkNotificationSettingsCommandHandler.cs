using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using QuantumBuild.Core.Application.Interfaces;
using QuantumBuild.Core.Application.Models;
using QuantumBuild.Modules.ToolboxTalks.Application.Common.Interfaces;
using QuantumBuild.Modules.ToolboxTalks.Application.DTOs;
using QuantumBuild.Modules.ToolboxTalks.Domain.Entities;

namespace QuantumBuild.Modules.ToolboxTalks.Application.Commands.UpdateToolboxTalkNotificationSettings;

public class UpdateToolboxTalkNotificationSettingsCommandHandler
    : IRequestHandler<UpdateToolboxTalkNotificationSettingsCommand, Result<ToolboxTalkSettingsDto>>
{
    private readonly IToolboxTalksDbContext _dbContext;
    private readonly ICurrentUserService _currentUser;
    private readonly IValidator<UpdateToolboxTalkNotificationSettingsCommand> _validator;

    public UpdateToolboxTalkNotificationSettingsCommandHandler(
        IToolboxTalksDbContext dbContext,
        ICurrentUserService currentUser,
        IValidator<UpdateToolboxTalkNotificationSettingsCommand> validator)
    {
        _dbContext = dbContext;
        _currentUser = currentUser;
        _validator = validator;
    }

    public async Task<Result<ToolboxTalkSettingsDto>> Handle(
        UpdateToolboxTalkNotificationSettingsCommand request, CancellationToken ct)
    {
        var validation = await _validator.ValidateAsync(request, ct);
        if (!validation.IsValid)
            return Result.Fail<ToolboxTalkSettingsDto>(
                string.Join("; ", validation.Errors.Select(e => e.ErrorMessage)));

        var settings = await _dbContext.ToolboxTalkSettings
            .Where(s => s.TenantId == request.TenantId && !s.IsDeleted)
            .FirstOrDefaultAsync(ct);

        if (settings is null)
        {
            settings = new ToolboxTalkSettings
            {
                Id = Guid.NewGuid(),
                TenantId = request.TenantId,
                CreatedBy = _currentUser.UserId ?? "system",
                CreatedAt = DateTime.UtcNow,
            };
            _dbContext.ToolboxTalkSettings.Add(settings);
        }

        settings.NotifyOnTranslationComplete = request.NotifyOnTranslationComplete;
        settings.NotifyOnValidationComplete = request.NotifyOnValidationComplete;
        settings.NotifyOnFailure = request.NotifyOnFailure;
        settings.NotifyOnExternalReviewResponse = request.NotifyOnExternalReviewResponse;
        settings.UpdatedAt = DateTime.UtcNow;
        settings.UpdatedBy = _currentUser.UserId;

        await _dbContext.SaveChangesAsync(ct);

        return Result.Ok(new ToolboxTalkSettingsDto
        {
            Id = settings.Id,
            TenantId = settings.TenantId,
            DefaultDueDays = settings.DefaultDueDays,
            ReminderFrequencyDays = settings.ReminderFrequencyDays,
            MaxReminders = settings.MaxReminders,
            EscalateAfterReminders = settings.EscalateAfterReminders,
            RequireVideoCompletion = settings.RequireVideoCompletion,
            DefaultPassingScore = settings.DefaultPassingScore,
            EnableTranslation = settings.EnableTranslation,
            TranslationProvider = settings.TranslationProvider,
            EnableVideoDubbing = settings.EnableVideoDubbing,
            VideoDubbingProvider = settings.VideoDubbingProvider,
            NotificationEmailTemplate = settings.NotificationEmailTemplate,
            ReminderEmailTemplate = settings.ReminderEmailTemplate,
            DefaultMinimumVideoWatchPercent = settings.DefaultMinimumVideoWatchPercent,
            DefaultAutoAssignDueDays = settings.DefaultAutoAssignDueDays,
            DefaultGenerateCertificate = settings.DefaultGenerateCertificate,
            DefaultRefresherFrequency = settings.DefaultRefresherFrequency,
            DefaultIsActive = settings.DefaultIsActive,
            NotifyOnTranslationComplete = settings.NotifyOnTranslationComplete,
            NotifyOnValidationComplete = settings.NotifyOnValidationComplete,
            NotifyOnFailure = settings.NotifyOnFailure,
            NotifyOnExternalReviewResponse = settings.NotifyOnExternalReviewResponse,
        });
    }
}
