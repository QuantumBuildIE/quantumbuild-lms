using QuantumBuild.Core.Domain.Common;

namespace QuantumBuild.Core.Domain.Entities;

public class TenantSetting : BaseEntity
{
    public Guid TenantId { get; set; }
    public string Module { get; set; } = string.Empty;
    public string Key { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
}
