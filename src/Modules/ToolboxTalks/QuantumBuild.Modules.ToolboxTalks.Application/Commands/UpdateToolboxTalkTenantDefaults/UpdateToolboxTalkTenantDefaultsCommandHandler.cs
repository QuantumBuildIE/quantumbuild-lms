using MediatR;
using Microsoft.EntityFrameworkCore;
using QuantumBuild.Core.Application.Interfaces;
using QuantumBuild.Core.Application.Models;
using QuantumBuild.Modules.ToolboxTalks.Application.Common.Interfaces;
using QuantumBuild.Modules.ToolboxTalks.Application.DTOs;
using QuantumBuild.Modules.ToolboxTalks.Domain.Entities;

namespace QuantumBuild.Modules.ToolboxTalks.Application.Commands.UpdateToolboxTalkTenantDefaults;

public class UpdateToolboxTalkTenantDefaultsCommandHandler
    : IRequestHandler<UpdateToolboxTalkTenantDefaultsCommand, Result<ToolboxTalkSettingsDto>>
{
    private readonly IToolboxTalksDbContext _dbContext;
    private readonly ICurrentUserService _currentUser;

    public UpdateToolboxTalkTenantDefaultsCommandHandler(
        IToolboxTalksDbContext dbContext,
        ICurrentUserService currentUser)
    {
        _dbContext = dbContext;
        _currentUser = currentUser;
    }

    // Validation now runs in the MediatR ValidationBehavior pipeline before Handle() is
    // ever called, so a manual IValidator invocation here would be dead code.
    public async Task<Result<ToolboxTalkSettingsDto>> Handle(
        UpdateToolboxTalkTenantDefaultsCommand request, CancellationToken ct)
    {
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

        settings.DefaultMinimumVideoWatchPercent = request.DefaultMinimumVideoWatchPercent;
        settings.DefaultAutoAssignDueDays = request.DefaultAutoAssignDueDays;
        settings.DefaultGenerateCertificate = request.DefaultGenerateCertificate;
        settings.DefaultRefresherFrequency = request.DefaultRefresherFrequency;
        settings.DefaultIsActive = request.DefaultIsActive;

        // Learning-wizard toggle defaults — optional/nullable: omitted fields preserve
        // the existing stored value rather than being reset.
        if (request.DefaultVideoRightsConfirmed.HasValue)
            settings.DefaultVideoRightsConfirmed = request.DefaultVideoRightsConfirmed.Value;
        if (request.DefaultUseQuestionPool.HasValue)
            settings.DefaultUseQuestionPool = request.DefaultUseQuestionPool.Value;
        if (request.DefaultGenerateSlideshow.HasValue)
            settings.DefaultGenerateSlideshow = request.DefaultGenerateSlideshow.Value;
        if (request.DefaultAutoAssign.HasValue)
            settings.DefaultAutoAssign = request.DefaultAutoAssign.Value;
        if (request.DefaultPreserveSourceWording.HasValue)
            settings.DefaultPreserveSourceWording = request.DefaultPreserveSourceWording.Value;
        if (request.DefaultShuffleQuestions.HasValue)
            settings.DefaultShuffleQuestions = request.DefaultShuffleQuestions.Value;
        if (request.DefaultShuffleOptions.HasValue)
            settings.DefaultShuffleOptions = request.DefaultShuffleOptions.Value;
        if (request.DefaultIncludeQuiz.HasValue)
            settings.DefaultIncludeQuiz = request.DefaultIncludeQuiz.Value;
        if (request.DefaultAllowRetry.HasValue)
            settings.DefaultAllowRetry = request.DefaultAllowRetry.Value;

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
            DefaultVideoRightsConfirmed = settings.DefaultVideoRightsConfirmed,
            DefaultUseQuestionPool = settings.DefaultUseQuestionPool,
            DefaultGenerateSlideshow = settings.DefaultGenerateSlideshow,
            DefaultAutoAssign = settings.DefaultAutoAssign,
            DefaultPreserveSourceWording = settings.DefaultPreserveSourceWording,
            DefaultShuffleQuestions = settings.DefaultShuffleQuestions,
            DefaultShuffleOptions = settings.DefaultShuffleOptions,
            DefaultIncludeQuiz = settings.DefaultIncludeQuiz,
            DefaultAllowRetry = settings.DefaultAllowRetry,
        });
    }
}
