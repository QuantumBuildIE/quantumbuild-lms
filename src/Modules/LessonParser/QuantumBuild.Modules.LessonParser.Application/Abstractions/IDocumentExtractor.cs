namespace QuantumBuild.Modules.LessonParser.Application.Abstractions;

/// <summary>
/// Extracts text content from various document sources (PDF, DOCX, URL, plain text)
/// </summary>
public interface IDocumentExtractor
{
    Task<ExtractionResult> ExtractFromPdfAsync(Stream pdfStream,
        string fileName,
        CancellationToken cancellationToken = default);

    Task<ExtractionResult> ExtractFromDocxAsync(Stream docxStream,
        string fileName,
        CancellationToken cancellationToken = default);

    Task<ExtractionResult> ExtractFromUrlAsync(string url,
        CancellationToken cancellationToken = default);

    Task<ExtractionResult> ExtractFromTextAsync(string content,
        string title,
        CancellationToken cancellationToken = default);
}
