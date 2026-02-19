using Microsoft.EntityFrameworkCore;
using QuantumBuild.Core.Application.Interfaces;
using QuantumBuild.Core.Domain.Entities;

namespace QuantumBuild.Core.Application.Features.TenantSettings;

public class TenantSettingsService(ICoreDbContext context) : ITenantSettingsService
{
    public async Task<string> GetSettingAsync(Guid tenantId, string key, string defaultValue, CancellationToken ct = default)
    {
        var setting = await context.TenantSettings
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.TenantId == tenantId && s.Key == key, ct);

        return setting?.Value ?? defaultValue;
    }

    public async Task SetSettingAsync(Guid tenantId, string key, string value, CancellationToken ct = default)
    {
        var setting = await context.TenantSettings
            .FirstOrDefaultAsync(s => s.TenantId == tenantId && s.Key == key, ct);

        if (setting == null)
        {
            setting = new TenantSetting
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                Module = TenantSettingKeys.GeneralModule,
                Key = key,
                Value = value
            };
            context.TenantSettings.Add(setting);
        }
        else
        {
            setting.Value = value;
        }

        await context.SaveChangesAsync(ct);
    }

    public async Task<Dictionary<string, string>> GetAllSettingsAsync(Guid tenantId, CancellationToken ct = default)
    {
        var settings = await context.TenantSettings
            .AsNoTracking()
            .Where(s => s.TenantId == tenantId)
            .ToListAsync(ct);

        // Start with defaults, then overlay saved values
        var result = new Dictionary<string, string>
        {
            [TenantSettingKeys.EmailTeamName] = TenantSettingKeys.Defaults.EmailTeamName,
            [TenantSettingKeys.TalkCertificatePrefix] = TenantSettingKeys.Defaults.TalkCertificatePrefix,
            [TenantSettingKeys.CourseCertificatePrefix] = TenantSettingKeys.Defaults.CourseCertificatePrefix
        };

        foreach (var setting in settings)
        {
            result[setting.Key] = setting.Value;
        }

        return result;
    }
}
