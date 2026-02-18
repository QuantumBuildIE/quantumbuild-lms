using QuantumBuild.Core.Domain.Common;

namespace QuantumBuild.Core.Domain.Entities;

public class LookupValue : BaseEntity
{
    public Guid CategoryId { get; set; }
    public LookupCategory Category { get; set; } = null!;
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Metadata { get; set; }
    public int SortOrder { get; set; }
    public bool IsActive { get; set; } = true;
}
