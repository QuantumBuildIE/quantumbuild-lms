using QuantumBuild.Core.Application.Models;

namespace QuantumBuild.Core.Application.Features.TenantSettings;

public interface ITenantSettingsService
{
    Task<string> GetSettingAsync(Guid tenantId, string key, string defaultValue, CancellationToken ct = default);
    Task SetSettingAsync(Guid tenantId, string key, string value, CancellationToken ct = default);
    Task<Dictionary<string, string>> GetAllSettingsAsync(Guid tenantId, CancellationToken ct = default);
}
