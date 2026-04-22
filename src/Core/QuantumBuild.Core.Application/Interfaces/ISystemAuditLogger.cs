namespace QuantumBuild.Core.Application.Interfaces;

public interface ISystemAuditLogger
{
    Task LogAsync(
        string action,
        bool success,
        string? entityType = null,
        Guid? entityId = null,
        string? entityDisplayName = null,
        string? failureReason = null,
        string? ipAddress = null,
        string? metadataJson = null,
        CancellationToken cancellationToken = default);
}
