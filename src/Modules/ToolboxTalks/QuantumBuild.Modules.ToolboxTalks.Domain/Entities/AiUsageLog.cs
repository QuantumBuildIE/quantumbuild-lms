using QuantumBuild.Core.Domain.Common;
using QuantumBuild.Modules.ToolboxTalks.Domain.Enums;

namespace QuantumBuild.Modules.ToolboxTalks.Domain.Entities;

/// <summary>
/// Raw log of each AI API call for usage tracking and cost attribution.
/// </summary>
public class AiUsageLog : TenantEntity
{
    /// <summary>
    /// The user who initiated the operation — null for background/system jobs
    /// </summary>
    public Guid? UserId { get; set; }

    public AiOperationCategory OperationCategory { get; set; }

    /// <summary>
    /// AI model identifier, e.g. "claude-sonnet-4-20250514"
    /// </summary>
    public string ModelId { get; set; } = string.Empty;

    public int InputTokens { get; set; }

    public int OutputTokens { get; set; }

    /// <summary>
    /// UTC timestamp of when the API call was made
    /// </summary>
    public DateTimeOffset CalledAt { get; set; }

    /// <summary>
    /// True when called from a Hangfire background job
    /// </summary>
    public bool IsSystemCall { get; set; }

    /// <summary>
    /// Optional reference to the entity being processed, e.g. ToolboxTalkId
    /// </summary>
    public Guid? ReferenceEntityId { get; set; }
}
