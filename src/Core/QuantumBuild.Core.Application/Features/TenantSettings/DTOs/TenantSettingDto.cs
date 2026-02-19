namespace QuantumBuild.Core.Application.Features.TenantSettings.DTOs;

public record TenantSettingDto(string Key, string Value);

public record UpdateTenantSettingsDto(List<TenantSettingDto> Settings);
