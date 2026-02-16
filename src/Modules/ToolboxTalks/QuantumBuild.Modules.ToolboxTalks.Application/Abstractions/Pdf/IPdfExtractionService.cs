namespace QuantumBuild.Modules.ToolboxTalks.Application.Abstractions.Pdf;

/// <summary>
/// Service for extracting text content from PDF documents.
/// Used to enable AI generation of toolbox talk sections and quiz questions from uploaded PDFs.
/// </summary>
public interface IPdfExtractionService
{
    /// <summary>
    /// Extracts all text content from a PDF file stream.
    /// Preserves page breaks and structure for better AI processing.
    /// </summary>
    /// <param name="pdfStream">The PDF file stream</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Extraction result with text content and page count</returns>
    Task<PdfExtractionResult> ExtractTextAsync(
        Stream pdfStream,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Extracts text from a PDF stored in R2 storage.
    /// Uses the PdfUrl stored in the ToolboxTalk entity to fetch and extract.
    /// </summary>
    /// <param name="pdfUrl">The public URL to the PDF in R2 storage</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Extraction result with text content and page count</returns>
    Task<PdfExtractionResult> ExtractTextFromUrlAsync(
        string pdfUrl,
        CancellationToken cancellationToken = default);
}
