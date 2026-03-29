using QuantumBuild.Core.Domain.Common;
using QuantumBuild.Modules.ToolboxTalks.Domain.Enums;

namespace QuantumBuild.Modules.ToolboxTalks.Domain.Entities;

/// <summary>
/// Daily aggregated AI usage summary per tenant, operation category, and model.
/// </summary>
public class AiUsageSummary : TenantEntity
{
    /// <summary>
    /// The day being summarised
    /// </summary>
    public DateOnly Date { get; set; }

    public AiOperationCategory OperationCategory { get; set; }

    /// <summary>
    /// AI model identifier, e.g. "claude-sonnet-4-20250514"
    /// </summary>
    public string ModelId { get; set; } = string.Empty;

    public int TotalCalls { get; set; }

    public long TotalInputTokens { get; set; }

    public long TotalOutputTokens { get; set; }

    /// <summary>
    /// How many of TotalCalls were background/system calls
    /// </summary>
    public int SystemCallCount { get; set; }
}
