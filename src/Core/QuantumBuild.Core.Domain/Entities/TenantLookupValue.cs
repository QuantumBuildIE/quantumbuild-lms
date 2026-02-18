using QuantumBuild.Core.Domain.Common;

namespace QuantumBuild.Core.Domain.Entities;

public class TenantLookupValue : TenantEntity
{
    public Guid CategoryId { get; set; }
    public LookupCategory Category { get; set; } = null!;
    public Guid? LookupValueId { get; set; }
    public LookupValue? LookupValue { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Metadata { get; set; }
    public int SortOrder { get; set; }
    public bool IsEnabled { get; set; } = true;
}
