using Microsoft.Extensions.Logging;
using QuantumBuild.Modules.ToolboxTalks.Application.Abstractions;
using QuantumBuild.Modules.ToolboxTalks.Application.Common.Interfaces;
using QuantumBuild.Modules.ToolboxTalks.Domain.Entities;
using QuantumBuild.Modules.ToolboxTalks.Domain.Enums;

namespace QuantumBuild.Modules.ToolboxTalks.Infrastructure.Services;

public class AiUsageLogger(
    IToolboxTalksDbContext dbContext,
    ILogger<AiUsageLogger> logger) : IAiUsageLogger
{
    public async Task LogAsync(
        Guid tenantId,
        AiOperationCategory category,
        string modelId,
        int inputTokens,
        int outputTokens,
        bool isSystemCall,
        Guid? userId = null,
        Guid? referenceEntityId = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var log = new AiUsageLog
            {
                TenantId = tenantId,
                OperationCategory = category,
                ModelId = modelId,
                InputTokens = inputTokens,
                OutputTokens = outputTokens,
                CalledAt = DateTimeOffset.UtcNow,
                IsSystemCall = isSystemCall,
                UserId = userId,
                ReferenceEntityId = referenceEntityId
            };

            dbContext.AiUsageLogs.Add(log);
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to log AI usage for {Category} / {ModelId} (tenant {TenantId})",
                category, modelId, tenantId);
        }
    }
}
