using QuantumBuild.Core.Application.Models;

namespace QuantumBuild.Core.Application.Features.Tenants;

public interface ITenantOnboardingService
{
    Task<Result> ProvisionTenantAsync(Guid tenantId, string contactEmail, string contactName);
}
