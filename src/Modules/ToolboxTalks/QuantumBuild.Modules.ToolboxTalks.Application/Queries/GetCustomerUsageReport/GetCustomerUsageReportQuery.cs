using MediatR;
using QuantumBuild.Modules.ToolboxTalks.Application.DTOs;

namespace QuantumBuild.Modules.ToolboxTalks.Application.Queries.GetCustomerUsageReport;

/// <summary>
/// Cross-tenant query returning one usage row per tenant.
/// SuperUser-only — handler uses IgnoreQueryFilters() throughout.
/// </summary>
public record GetCustomerUsageReportQuery : IRequest<CustomerUsageReportDto>
{
    /// <summary>
    /// Baseline date for "new learnings" and "completions" counts.
    /// Defaults to the stored LastReviewedAt; falls back to 30 days ago if never reviewed.
    /// </summary>
    public DateTimeOffset? ComparisonDate { get; init; }
}
