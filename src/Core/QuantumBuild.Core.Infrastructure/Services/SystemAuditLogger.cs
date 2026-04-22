using Microsoft.Extensions.Logging;
using QuantumBuild.Core.Application.Interfaces;
using QuantumBuild.Core.Domain.Entities;

namespace QuantumBuild.Core.Infrastructure.Services;

public class SystemAuditLogger(
    ICoreDbContext db,
    ICurrentUserService currentUser,
    ILogger<SystemAuditLogger> logger) : ISystemAuditLogger
{
    public async Task LogAsync(
        string action,
        bool success,
        string? entityType = null,
        Guid? entityId = null,
        string? entityDisplayName = null,
        string? failureReason = null,
        string? ipAddress = null,
        string? metadataJson = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var userId = currentUser.UserIdGuid == Guid.Empty ? (Guid?)null : currentUser.UserIdGuid;
            var tenantId = currentUser.TenantId == Guid.Empty ? (Guid?)null : currentUser.TenantId;
            var userName = string.IsNullOrEmpty(currentUser.UserName) ? null : currentUser.UserName;

            var log = new SystemAuditLog
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                UserName = userName,
                TenantId = tenantId,
                Action = action,
                EntityType = entityType,
                EntityId = entityId,
                EntityDisplayName = entityDisplayName,
                IpAddress = ipAddress,
                Success = success,
                FailureReason = failureReason,
                OccurredAt = DateTimeOffset.UtcNow,
                MetadataJson = metadataJson
            };

            db.SystemAuditLogs.Add(log);
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to write system audit log for action {Action}", action);
        }
    }
}
