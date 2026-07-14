using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using Microsoft.Extensions.Logging;
using QuantumBuild.Modules.ToolboxTalks.Application.Abstractions.Pdf;

namespace QuantumBuild.Modules.ToolboxTalks.Infrastructure.Services.ContentCreation;

/// <summary>
/// Extracts plain text from Word documents (.docx) using the OpenXML SDK.
/// Downloads the file from a URL and walks all paragraph descendants of the document body.
/// </summary>
public class DocxExtractionService : IDocxExtractionService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<DocxExtractionService> _logger;

    public DocxExtractionService(HttpClient httpClient, ILogger<DocxExtractionService> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<DocxExtractionResult> ExtractTextFromUrlAsync(
        string docxUrl,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(docxUrl))
            return new DocxExtractionResult(false, null, "Word document URL is required.");

        try
        {
            _logger.LogInformation("Downloading Word document from URL: {Url}", docxUrl);

            using var response = await _httpClient.GetAsync(docxUrl, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("Failed to download Word document. Status: {StatusCode}", response.StatusCode);
                return new DocxExtractionResult(false, null,
                    $"Failed to download Word document. HTTP status: {response.StatusCode}");
            }

            await using var docxStream = await response.Content.ReadAsStreamAsync(cancellationToken);

            // OpenXML SDK requires a seekable stream
            using var memoryStream = new MemoryStream();
            await docxStream.CopyToAsync(memoryStream, cancellationToken);
            memoryStream.Position = 0;

            return ExtractTextFromStream(memoryStream);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "HTTP error while downloading Word document from {Url}", docxUrl);
            return new DocxExtractionResult(false, null, $"Failed to download Word document: {ex.Message}");
        }
        catch (TaskCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (TaskCanceledException ex)
        {
            _logger.LogError(ex, "Request timed out while downloading Word document from {Url}", docxUrl);
            return new DocxExtractionResult(false, null,
                "Word document download timed out. The file may be too large.");
        }
    }

    private DocxExtractionResult ExtractTextFromStream(Stream stream)
    {
        try
        {
            using var wordDoc = WordprocessingDocument.Open(stream, false);
            var body = wordDoc.MainDocumentPart?.Document?.Body;

            if (body is null)
                return new DocxExtractionResult(false, null,
                    "The Word document appears to be empty or could not be read.");

            var paragraphs = body.Descendants<Paragraph>()
                .Select(p => p.InnerText.Trim())
                .Where(t => !string.IsNullOrEmpty(t));

            var text = string.Join("\n\n", paragraphs).Trim();

            if (string.IsNullOrWhiteSpace(text) || text.Length < 50)
                return new DocxExtractionResult(false, null,
                    "The Word document appears to be empty or contains too little text to extract.");

            _logger.LogInformation(
                "Successfully extracted {CharCount} characters from Word document", text.Length);

            return new DocxExtractionResult(true, text, null);
        }
        catch (Exception ex) when (
            ex.Message.Contains("password", StringComparison.OrdinalIgnoreCase) ||
            ex.Message.Contains("encrypted", StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogWarning(ex, "Word document is password-protected");
            return new DocxExtractionResult(false, null,
                "The Word document is password-protected. Please remove the password and try again.");
        }
        catch (Exception ex) when (ex is IOException || ex is InvalidDataException)
        {
            _logger.LogError(ex, "Word document could not be read — corrupted or unsupported format");
            return new DocxExtractionResult(false, null,
                "The Word document could not be read. The file may be corrupted or in an unsupported format. " +
                "Note: legacy .doc files are not supported — please save as .docx.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error while extracting text from Word document");
            return new DocxExtractionResult(false, null,
                "Could not extract content from the Word document.");
        }
    }
}
