using QuantumBuild.Core.Application.Models;

namespace QuantumBuild.Modules.ToolboxTalks.Application.Services;

/// <summary>
/// Service for generating an animated HTML slideshow from a PDF document using AI.
/// Claude analyzes the PDF content and generates a complete, self-contained HTML file
/// with animations, gradients, and curated safety information.
/// </summary>
public interface IAiSlideshowGenerationService
{
    /// <summary>
    /// Generates an HTML slideshow from a PDF using AI.
    /// </summary>
    /// <param name="pdfBytes">The PDF file content</param>
    /// <param name="documentTitle">Title to use for the slideshow</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Complete HTML string for the slideshow</returns>
    Task<Result<string>> GenerateSlideshowFromPdfAsync(
        byte[] pdfBytes,
        string documentTitle,
        CancellationToken cancellationToken = default);
}
