using QuantumBuild.Modules.ToolboxTalks.Domain.Enums;

namespace QuantumBuild.Modules.ToolboxTalks.Application.DTOs.Validation;

/// <summary>
/// Status of a regulatory document ingestion session
/// </summary>
public record IngestionSessionDto
{
    public Guid RegulatoryDocumentId { get; init; }
    public string DocumentTitle { get; init; } = string.Empty;
    public string? SourceUrl { get; init; }

    /// <summary>
    /// One of "Idle", "Ingesting", "Success", "Failed" — mirrors
    /// RegulatoryDocument.LastIngestionStatus verbatim.
    /// </summary>
    public string Status { get; init; } = string.Empty;
    public DateTimeOffset? LastIngestedAt { get; init; }

    /// <summary>Populated only when Status is "Failed".</summary>
    public string? LastIngestionErrorMessage { get; init; }

    /// <summary>Populated only when Status is "Failed": "invalid_uri", "fetch_failed", "parse_failed", or "unknown".</summary>
    public string? LastIngestionErrorCode { get; init; }

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

// ─── Tenant-admin browse DTOs ─────────────────────────────────────────────

/// <summary>
/// Top-level browse node: a regulatory body and its documents relevant to the caller's sectors
/// </summary>
public record RegulatoryBrowseBodyDto
{
    public Guid Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public string Code { get; init; } = string.Empty;
    public string? Country { get; init; }
    public List<RegulatoryBrowseDocumentDto> Documents { get; init; } = new();
}

public record RegulatoryBrowseDocumentDto
{
    public Guid Id { get; init; }
    public string Title { get; init; } = string.Empty;
    public string? Version { get; init; }
    public List<string> SectorKeys { get; init; } = new();
    public List<RegulatoryBrowsePrincipleGroupDto> PrincipleGroups { get; init; } = new();
}

public record RegulatoryBrowsePrincipleGroupDto
{
    public string? Principle { get; init; }
    public string? PrincipleLabel { get; init; }
    public List<RegulatoryBrowseRequirementDto> Requirements { get; init; } = new();
}

public record RegulatoryBrowseRequirementDto
{
    public Guid Id { get; init; }
    public string Title { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public string Priority { get; init; } = "med";
    public string? Section { get; init; }
    public string? SectionLabel { get; init; }
    public string SectorKey { get; init; } = string.Empty;
    public string SectorName { get; init; } = string.Empty;
}

/// <summary>
/// Response after uploading a source PDF for a regulatory document
/// </summary>
public record RegulatoryDocumentUploadResponseDto
{
    public string SourceUrl { get; init; } = string.Empty;
    public string FileName { get; init; } = string.Empty;
    public long FileSizeBytes { get; init; }
}

/// <summary>
/// A regulatory body available as a picker option when creating a new regulatory document,
/// or a row in the admin catalog list. Kind/SectorId/SectorName let the frontend distinguish
/// Regulation bodies (SectorId always null) from Standard bodies (SectorId always populated).
/// </summary>
public record RegulatoryBodyDto
{
    public Guid Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public string Code { get; init; } = string.Empty;
    public string Country { get; init; } = string.Empty;
    public string Kind { get; init; } = string.Empty;
    public Guid? SectorId { get; init; }
    public string? SectorName { get; init; }
}

/// <summary>
/// Request to create a new regulatory body (catalog entry) — either a Regulation (SectorId
/// must be null) or a Standard (SectorId required). Enforced at the service layer and by a DB
/// check constraint (see RegulatoryBodyConfiguration).
/// </summary>
public record CreateRegulatoryBodyRequest
{
    public string Name { get; init; } = string.Empty;
    public string Code { get; init; } = string.Empty;
    public string Country { get; init; } = string.Empty;
    public string? Website { get; init; }
    public RegulatoryBodyKind Kind { get; init; } = RegulatoryBodyKind.Regulation;
    public Guid? SectorId { get; init; }
}

/// <summary>
/// Request to create a new regulatory document. Persists with LastIngestionStatus=Idle —
/// ingestion remains a separate, explicit action on the document's detail page.
/// </summary>
public record CreateRegulatoryDocumentRequest
{
    public Guid RegulatoryBodyId { get; init; }
    public string Title { get; init; } = string.Empty;
    public string Version { get; init; } = string.Empty;
    public string? SourceUrl { get; init; }
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
