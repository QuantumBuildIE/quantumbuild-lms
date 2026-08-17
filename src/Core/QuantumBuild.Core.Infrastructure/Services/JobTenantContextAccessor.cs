using QuantumBuild.Core.Application.Interfaces;

namespace QuantumBuild.Core.Infrastructure.Services;

public sealed class JobTenantContextAccessor : IJobTenantContextAccessor
{
    public Guid? TenantId { get; set; }
}
