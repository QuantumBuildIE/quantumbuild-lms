using QuantumBuild.Modules.ToolboxTalks.Domain.Enums;

namespace QuantumBuild.Modules.ToolboxTalks.Application.Abstractions;

public interface IAiUsageLogger
{
    Task LogAsync(
        Guid tenantId,
        AiOperationCategory category,
        string modelId,
        int inputTokens,
        int outputTokens,
        bool isSystemCall,
        Guid? userId = null,
        Guid? referenceEntityId = null,
        CancellationToken cancellationToken = default);
}
