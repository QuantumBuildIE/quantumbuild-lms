using QuantumBuild.Core.Domain.Common;

namespace QuantumBuild.Modules.ToolboxTalks.Domain.Entities;

/// <summary>
/// System-level singleton tracking SuperUser review state for the customer usage report.
/// No TenantId — shared across all tenants. One logical row ever created.
/// </summary>
public class CustomerUsageReportState : BaseEntity
{
    /// <summary>
    /// UTC timestamp of the most recent SuperUser mark-reviewed action.
    /// Null until the first review is recorded.
    /// </summary>
    public DateTimeOffset? LastReviewedAt { get; set; }
}
