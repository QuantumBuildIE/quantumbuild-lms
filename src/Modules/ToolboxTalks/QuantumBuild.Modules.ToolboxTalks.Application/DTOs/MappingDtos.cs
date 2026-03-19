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
