using MediatR;
using Microsoft.EntityFrameworkCore;
using QuantumBuild.Modules.ToolboxTalks.Application.Common.Interfaces;
using QuantumBuild.Modules.ToolboxTalks.Application.DTOs;

namespace QuantumBuild.Modules.ToolboxTalks.Application.Queries.GetToolboxTalkSettings;

public class GetToolboxTalkSettingsQueryHandler : IRequestHandler<GetToolboxTalkSettingsQuery, ToolboxTalkSettingsDto>
{
    private readonly IToolboxTalksDbContext _context;

    public GetToolboxTalkSettingsQueryHandler(IToolboxTalksDbContext context)
    {
        _context = context;
    }

    public async Task<ToolboxTalkSettingsDto> Handle(GetToolboxTalkSettingsQuery request, CancellationToken cancellationToken)
    {
        var settings = await _context.ToolboxTalkSettings
            .Where(s => s.TenantId == request.TenantId && !s.IsDeleted)
            .FirstOrDefaultAsync(cancellationToken);

        if (settings != null)
        {
            return new ToolboxTalkSettingsDto
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
                ReminderEmailTemplate = settings.ReminderEmailTemplate
            };
        }

        // Return default settings if none exist
        return new ToolboxTalkSettingsDto
        {
            Id = Guid.Empty,
            TenantId = request.TenantId,
            DefaultDueDays = 7,
            ReminderFrequencyDays = 2,
            MaxReminders = 3,
            EscalateAfterReminders = 2,
            RequireVideoCompletion = true,
            DefaultPassingScore = 80,
            EnableTranslation = false,
            TranslationProvider = null,
            EnableVideoDubbing = false,
            VideoDubbingProvider = null,
            NotificationEmailTemplate = null,
            ReminderEmailTemplate = null
        };
    }
}
