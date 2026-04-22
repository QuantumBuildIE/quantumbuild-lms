namespace QuantumBuild.Core.Domain.Entities;

public class SystemAuditLog
{
    public Guid Id { get; set; }

    // Who
    public Guid? UserId { get; set; }
    public string? UserName { get; set; }
    public Guid? TenantId { get; set; }

    // What
    public string Action { get; set; } = string.Empty;
    public string? EntityType { get; set; }
    public Guid? EntityId { get; set; }
    public string? EntityDisplayName { get; set; }

    // Context
    public string? IpAddress { get; set; }
    public bool Success { get; set; }
    public string? FailureReason { get; set; }

    // When
    public DateTimeOffset OccurredAt { get; set; }

    // Optional detail
    public string? MetadataJson { get; set; }
}
