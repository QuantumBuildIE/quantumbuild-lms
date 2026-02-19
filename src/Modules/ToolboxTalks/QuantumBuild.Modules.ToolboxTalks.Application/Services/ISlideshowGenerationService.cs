using QuantumBuild.Core.Application.Models;

namespace QuantumBuild.Modules.ToolboxTalks.Application.Services;

/// <summary>
/// Service for generating an AI-powered HTML slideshow from the PDF or video transcript
/// attached to a toolbox talk.
/// </summary>
public interface ISlideshowGenerationService
{
    /// <summary>
    /// Generates an AI-powered HTML slideshow from the talk's PDF or video transcript.
    /// Returns the generated HTML string on success.
    /// </summary>
    /// <param name="tenantId">The tenant ID</param>
    /// <param name="toolboxTalkId">The toolbox talk ID</param>
    /// <param name="source">Content source: "pdf" (default) or "video"</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task<Result<string>> GenerateSlideshowAsync(
        Guid tenantId,
        Guid toolboxTalkId,
        string source = "pdf",
        CancellationToken cancellationToken = default);
}
