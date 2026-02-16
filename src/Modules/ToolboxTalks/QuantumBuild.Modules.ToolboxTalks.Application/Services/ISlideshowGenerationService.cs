using QuantumBuild.Core.Application.Models;

namespace QuantumBuild.Modules.ToolboxTalks.Application.Services;

/// <summary>
/// Service for generating an AI-powered HTML slideshow from the PDF attached to a toolbox talk.
/// Downloads the PDF, sends it to AI for analysis, and stores the generated HTML slideshow.
/// </summary>
public interface ISlideshowGenerationService
{
    /// <summary>
    /// Generates an AI-powered HTML slideshow from the talk's PDF.
    /// Returns the generated HTML string on success.
    /// </summary>
    Task<Result<string>> GenerateSlideshowAsync(
        Guid tenantId,
        Guid toolboxTalkId,
        CancellationToken cancellationToken = default);
}
