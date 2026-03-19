namespace QuantumBuild.Modules.ToolboxTalks.Application.DTOs;

/// <summary>
/// A pending or reviewed regulatory requirement mapping for the admin review UI
/// </summary>
public record PendingMappingDto(
    Guid Id,
    Guid RegulatoryRequirementId,
    string RequirementTitle,
    string RequirementDescription,
    string? RequirementSection,
    string? RequirementSectionLabel,
    string? RequirementPrinciple,
    string? RequirementPrincipleLabel,
    string RequirementPriority,
    int? ConfidenceScore,
    string? AiReasoning,
    string? ReviewNotes,
    string MappingStatus,
    string ContentTitle,
    string ContentType,
    Guid ContentId,
    DateTimeOffset CreatedAt
);

/// <summary>
/// Request to confirm a mapping
/// </summary>
public record ConfirmMappingRequest(Guid MappingId);

/// <summary>
/// Request to reject a mapping
/// </summary>
public record RejectMappingRequest(Guid MappingId, string? Notes);

/// <summary>
/// Summary of mapping statuses for the review dashboard
/// </summary>
public record MappingSummaryDto(
    int TotalSuggested,
    int TotalConfirmed,
    int TotalRejected,
    List<PendingMappingDto> PendingReview
);

// ============================================
// Compliance Checklist DTOs
// ============================================

/// <summary>
/// Detail of a single mapping on a compliance requirement
/// </summary>
public record MappingDetailDto(
    Guid MappingId,
    Guid ContentId,
    string ContentTitle,
    string ContentType,
    string MappingStatus,
    int? ConfidenceScore,
    int? ValidationScore,
    string? ValidationOutcome,
    DateTimeOffset? ValidationDate
);

/// <summary>
/// A single regulatory requirement with its coverage status and mappings
/// </summary>
public record ComplianceRequirementDto(
    Guid Id,
    string Title,
    string Description,
    string? Section,
    string? SectionLabel,
    string? Principle,
    string? PrincipleLabel,
    string Priority,
    int DisplayOrder,
    string CoverageStatus,
    List<MappingDetailDto> Mappings
);

/// <summary>
/// Requirements grouped by principle
/// </summary>
public record CompliancePrincipleGroupDto(
    string Principle,
    string PrincipleLabel,
    int TotalRequirements,
    int CoveredCount,
    int PendingCount,
    int GapCount,
    List<ComplianceRequirementDto> Requirements
);

/// <summary>
/// Full compliance checklist response for a sector
/// </summary>
public record ComplianceChecklistDto(
    string SectorKey,
    string SectorName,
    string RegulatoryBody,
    string ScoreLabel,
    int TotalRequirements,
    int CoveredCount,
    int PendingCount,
    int GapCount,
    int CoveragePercentage,
    List<CompliancePrincipleGroupDto> PrincipleGroups,
    DateTimeOffset LastUpdated
);

/// <summary>
/// Request to create a manual (non-AI) mapping
/// </summary>
public record AddManualMappingRequest(
    Guid RegulatoryRequirementId,
    Guid? ToolboxTalkId,
    Guid? CourseId
);

/// <summary>
/// A talk or course available for manual mapping selection
/// </summary>
public record ContentOptionDto(
    Guid Id,
    string Title,
    string Type
);
