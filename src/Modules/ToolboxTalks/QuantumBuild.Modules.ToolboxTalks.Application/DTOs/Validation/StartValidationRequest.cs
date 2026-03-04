namespace QuantumBuild.Modules.ToolboxTalks.Application.DTOs.Validation;

/// <summary>
/// Request body for starting a translation validation run
/// </summary>
public record StartValidationRequest
{
    /// <summary>
    /// Target language code to validate (e.g., "es", "pt")
    /// </summary>
    public string LanguageCode { get; init; } = string.Empty;

    /// <summary>
    /// Safety glossary sector key (e.g., "construction", "mining")
    /// </summary>
    public string? SectorKey { get; init; }

    /// <summary>
    /// Pass threshold (0-100). If not provided, uses the system default.
    /// </summary>
    public int? PassThreshold { get; init; }

    /// <summary>
    /// Source language code (defaults to "en")
    /// </summary>
    public string SourceLanguage { get; init; } = "en";

    // Audit metadata
    public string? ReviewerName { get; init; }
    public string? ReviewerOrg { get; init; }
    public string? ReviewerRole { get; init; }
    public string? DocumentRef { get; init; }
    public string? ClientName { get; init; }
    public string? AuditPurpose { get; init; }
}
