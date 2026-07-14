namespace QuantumBuild.Modules.ToolboxTalks.Application.Abstractions.Pdf;

/// <summary>
/// Service for extracting text content from Word documents (.docx).
/// Used to enable AI generation of toolbox talk sections and quiz questions from uploaded Word documents.
/// </summary>
public interface IDocxExtractionService
{
    /// <summary>
    /// Extracts all text content from a Word document (.docx) stored at the given URL.
    /// Downloads the file, extracts paragraph text using the OpenXML SDK, and returns the result.
    /// </summary>
    /// <param name="docxUrl">The public URL to the Word document in R2 storage</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Extraction result with text content or an error message</returns>
    Task<DocxExtractionResult> ExtractTextFromUrlAsync(
        string docxUrl,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Result of a Word document text extraction operation.
/// </summary>
public record DocxExtractionResult(
    bool Success,
    string? Text,
    string? ErrorMessage);
