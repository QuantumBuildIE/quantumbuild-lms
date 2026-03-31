namespace QuantumBuild.Modules.ToolboxTalks.Application.DTOs.Validation;

/// <summary>
/// Request body for running a pre-flight scan on a talk's source content.
/// </summary>
public record PreFlightScanRequest
{
    /// <summary>
    /// Target language for the scan (e.g., "Polish", "Romanian")
    /// </summary>
    public string TargetLanguage { get; init; } = string.Empty;

    /// <summary>
    /// Optional sector key to prioritise sector-specific terminology
    /// </summary>
    public string? SectorKey { get; init; }
}
