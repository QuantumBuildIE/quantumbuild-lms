using QuantumBuild.Core.Domain.Common;
using QuantumBuild.Modules.ToolboxTalks.Domain.Enums;

namespace QuantumBuild.Modules.ToolboxTalks.Domain.Entities;

/// <summary>
/// Tenant-scoped mapping between a regulatory requirement and a talk or course.
/// Either ToolboxTalkId or CourseId must be set — never both, never neither.
/// MappingStatus drives the compliance checklist: Suggested (AI), Confirmed (reviewer), Rejected.
/// </summary>
public class RegulatoryRequirementMapping : TenantEntity
{
    public Guid RegulatoryRequirementId { get; set; }

    /// <summary>
    /// FK to ToolboxTalk — one or the other with CourseId, never both
    /// </summary>
    public Guid? ToolboxTalkId { get; set; }

    /// <summary>
    /// FK to ToolboxTalkCourse — one or the other with ToolboxTalkId, never both
    /// </summary>
    public Guid? CourseId { get; set; }

    public RequirementMappingStatus MappingStatus { get; set; } = RequirementMappingStatus.Suggested;

    /// <summary>
    /// AI confidence score (0-100) when mapping was AI-suggested
    /// </summary>
    public int? ConfidenceScore { get; set; }

    /// <summary>
    /// AI reasoning for why this mapping was suggested
    /// </summary>
    public string? AiReasoning { get; set; }

    /// <summary>
    /// Username of the reviewer who confirmed/rejected this mapping
    /// </summary>
    public string? ReviewedBy { get; set; }

    public DateTimeOffset? ReviewedAt { get; set; }

    /// <summary>
    /// Reviewer notes — rejection reason or confirmation comments. Separate from AiReasoning.
    /// </summary>
    public string? ReviewNotes { get; set; }

    // Navigation properties
    public RegulatoryRequirement RegulatoryRequirement { get; set; } = null!;
    public ToolboxTalk? ToolboxTalk { get; set; }
    public ToolboxTalkCourse? Course { get; set; }
}
