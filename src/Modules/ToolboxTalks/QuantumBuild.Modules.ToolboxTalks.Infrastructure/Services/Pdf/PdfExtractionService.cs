using System.Text;
using Microsoft.Extensions.Logging;
using QuantumBuild.Modules.ToolboxTalks.Application.Abstractions.Pdf;
using UglyToad.PdfPig;

namespace QuantumBuild.Modules.ToolboxTalks.Infrastructure.Services.Pdf;

/// <summary>
/// Service for extracting text content from PDF documents using PdfPig.
/// PdfPig is a fully managed .NET library with no native dependencies.
/// </summary>
public class PdfExtractionService : IPdfExtractionService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<PdfExtractionService> _logger;

    public PdfExtractionService(
        HttpClient httpClient,
        ILogger<PdfExtractionService> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<PdfExtractionResult> ExtractTextAsync(
        Stream pdfStream,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // Copy to memory stream since PdfPig requires a seekable stream
            using var memoryStream = new MemoryStream();
            await pdfStream.CopyToAsync(memoryStream, cancellationToken);
            memoryStream.Position = 0;

            using var document = PdfDocument.Open(memoryStream);
            var textBuilder = new StringBuilder();
            var pageCount = document.NumberOfPages;

            _logger.LogInformation("Extracting text from PDF with {PageCount} pages", pageCount);

            foreach (var page in document.GetPages())
            {
                cancellationToken.ThrowIfCancellationRequested();

                var pageText = page.Text;

                // Skip pages with no text (likely scanned images)
                if (string.IsNullOrWhiteSpace(pageText))
                {
                    _logger.LogDebug("Page {PageNumber} has no extractable text (may be scanned image)", page.Number);
                    textBuilder.AppendLine($"[Page {page.Number} - No extractable text]");
                }
                else
                {
                    textBuilder.AppendLine(pageText);
                }

                // Add page separator for AI processing context
                textBuilder.AppendLine();
                textBuilder.AppendLine($"--- End of Page {page.Number} ---");
                textBuilder.AppendLine();
            }

            var extractedText = textBuilder.ToString().Trim();

            // Check if we got any meaningful text
            if (string.IsNullOrWhiteSpace(extractedText) ||
                !extractedText.Any(c => char.IsLetter(c)))
            {
                _logger.LogWarning("PDF appears to be scanned with no extractable text. OCR may be required.");
                return PdfExtractionResult.FailureResult(
                    "This PDF appears to be a scanned document with no extractable text. " +
                    "OCR (Optical Character Recognition) would be needed to extract text from scanned pages.");
            }

            _logger.LogInformation(
                "Successfully extracted {CharCount} characters from {PageCount} pages",
                extractedText.Length, pageCount);

            return PdfExtractionResult.SuccessResult(extractedText, pageCount);
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("PDF extraction was cancelled");
            throw;
        }
        catch (Exception ex) when (
            ex.Message.Contains("password", StringComparison.OrdinalIgnoreCase) ||
            ex.Message.Contains("encrypted", StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogWarning(ex, "PDF is password protected or encrypted");
            return PdfExtractionResult.FailureResult(
                "This PDF is password protected. Please provide an unprotected PDF document.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to extract text from PDF");

            // Check for common PDF format issues
            if (ex.Message.Contains("not a valid PDF", StringComparison.OrdinalIgnoreCase) ||
                ex.Message.Contains("invalid", StringComparison.OrdinalIgnoreCase))
            {
                return PdfExtractionResult.FailureResult("Invalid PDF format. The file may be corrupted.");
            }

            return PdfExtractionResult.FailureResult($"Failed to extract text: {ex.Message}");
        }
    }

    /// <inheritdoc />
    public async Task<PdfExtractionResult> ExtractTextFromUrlAsync(
        string pdfUrl,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(pdfUrl))
        {
            return PdfExtractionResult.FailureResult("PDF URL is required");
        }

        try
        {
            _logger.LogInformation("Downloading PDF from URL: {Url}", pdfUrl);

            using var response = await _httpClient.GetAsync(pdfUrl, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("Failed to download PDF. Status: {StatusCode}", response.StatusCode);
                return PdfExtractionResult.FailureResult(
                    $"Failed to download PDF. HTTP status: {response.StatusCode}");
            }

            var contentType = response.Content.Headers.ContentType?.MediaType;
            if (contentType != null &&
                !contentType.Contains("pdf", StringComparison.OrdinalIgnoreCase) &&
                !contentType.Contains("octet-stream", StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogWarning("Unexpected content type: {ContentType}", contentType);
            }

            await using var pdfStream = await response.Content.ReadAsStreamAsync(cancellationToken);

            return await ExtractTextAsync(pdfStream, cancellationToken);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "HTTP error while downloading PDF from {Url}", pdfUrl);
            return PdfExtractionResult.FailureResult($"Failed to download PDF: {ex.Message}");
        }
        catch (TaskCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (TaskCanceledException ex)
        {
            _logger.LogError(ex, "Request timed out while downloading PDF from {Url}", pdfUrl);
            return PdfExtractionResult.FailureResult("PDF download timed out. The file may be too large.");
        }
    }
}
