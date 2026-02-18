using QuantumBuild.Core.Domain.Common;

namespace QuantumBuild.Core.Domain.Entities;

public class LookupCategory : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public string Module { get; set; } = string.Empty;
    public bool AllowCustom { get; set; } = true;
    public bool IsActive { get; set; } = true;

    public ICollection<LookupValue> Values { get; set; } = new List<LookupValue>();
    public ICollection<TenantLookupValue> TenantValues { get; set; } = new List<TenantLookupValue>();
}
