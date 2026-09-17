namespace QuantumBuild.Modules.ToolboxTalks.Infrastructure.Configuration;

/// <summary>
/// Configuration settings for AI-driven section generation from video/PDF/text content.
/// Binds to the "ContentGeneration" section in appsettings.json.
/// </summary>
public class ContentGenerationSettings
{
    /// <summary>
    /// Configuration section name in appsettings.json
    /// </summary>
    public const string SectionName = "ContentGeneration";

    /// <summary>
    /// Minimum number of sections the AI should generate from source content.
    /// A floor, not a target — it prevents the degenerate single-section case while
    /// letting the AI split further where the content warrants it.
    /// Default: 2
    /// </summary>
    public int MinimumSections { get; set; } = 2;
}
