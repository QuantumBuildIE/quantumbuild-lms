namespace QuantumBuild.Modules.ToolboxTalks.Application.DTOs.Validation;

/// <summary>
/// Status of a regulatory document ingestion session
/// </summary>
public record IngestionSessionDto
{
    public Guid RegulatoryDocumentId { get; init; }
    public string DocumentTitle { get; init; } = string.Empty;
    public string? SourceUrl { get; init; }
    public string Status { get; init; } = string.Empty;
    public DateTimeOffset? LastIngestedAt { get; init; }
    public int DraftCount { get; init; }
    public int ApprovedCount { get; init; }
    public int RejectedCount { get; init; }
}

/// <summary>
/// A draft regulatory requirement pending review
/// </summary>
public record DraftRequirementDto
{
    public Guid Id { get; init; }
    public string Title { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public string? Section { get; init; }
    public string? SectionLabel { get; init; }
    public string? Principle { get; init; }
    public string? PrincipleLabel { get; init; }
    public string Priority { get; init; } = "med";
    public int DisplayOrder { get; init; }
    public string IngestionSource { get; init; } = string.Empty;
    public string? IngestionNotes { get; init; }
    public string ProfileSectorKey { get; init; } = string.Empty;
    public string ProfileSectorName { get; init; } = string.Empty;
}

/// <summary>
/// A regulatory requirement with full status info
/// </summary>
public record RegulatoryRequirementDto
{
    public Guid Id { get; init; }
    public string Title { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public string? Section { get; init; }
    public string? SectionLabel { get; init; }
    public string? Principle { get; init; }
    public string? PrincipleLabel { get; init; }
    public string Priority { get; init; } = "med";
    public int DisplayOrder { get; init; }
    public string IngestionSource { get; init; } = string.Empty;
    public string IngestionStatus { get; init; } = string.Empty;
    public string? IngestionNotes { get; init; }
    public string ProfileSectorKey { get; init; } = string.Empty;
    public string ProfileSectorName { get; init; } = string.Empty;
}

/// <summary>
/// Request to approve a draft requirement (allows edits before approving)
/// </summary>
public record ApproveRequirementRequest
{
    public string Title { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public string? Section { get; init; }
    public string? SectionLabel { get; init; }
    public string? Principle { get; init; }
    public string? PrincipleLabel { get; init; }
    public string Priority { get; init; } = "med";
    public int DisplayOrder { get; init; }
}

/// <summary>
/// Request to update a draft requirement
/// </summary>
public record UpdateDraftRequirementRequest
{
    public string Title { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public string? Section { get; init; }
    public string? SectionLabel { get; init; }
    public string? Principle { get; init; }
    public string? PrincipleLabel { get; init; }
    public string Priority { get; init; } = "med";
    public int DisplayOrder { get; init; }
}

/// <summary>
/// Request to reject a draft requirement
/// </summary>
public record RejectRequirementRequest
{
    public string Notes { get; init; } = string.Empty;
}

/// <summary>
/// Regulatory document with body, profiles, and requirement counts
/// </summary>
public record RegulatoryDocumentListDto
{
    public Guid Id { get; init; }
    public string RegulatoryBodyName { get; init; } = string.Empty;
    public string RegulatoryBodyCode { get; init; } = string.Empty;
    public string Title { get; init; } = string.Empty;
    public string Version { get; init; } = string.Empty;
    public string? Source { get; init; }
    public string? SourceUrl { get; init; }
    public DateOnly? EffectiveDate { get; init; }
    public bool IsActive { get; init; }
    public DateTimeOffset? LastIngestedAt { get; init; }
    public List<string> SectorKeys { get; init; } = new();
    public int DraftCount { get; init; }
    public int ApprovedCount { get; init; }
    public int RejectedCount { get; init; }
}
