using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using DocumentFormat.OpenXml.Packaging;
using HtmlAgilityPack;
using Microsoft.Extensions.Logging;
using QuantumBuild.Modules.LessonParser.Application.Abstractions;
using UglyToad.PdfPig;

namespace QuantumBuild.Modules.LessonParser.Infrastructure.Services;

/// <summary>
/// Extracts text content from PDF, DOCX, URL, and plain text sources.
/// </summary>
public class DocumentExtractorService : IDocumentExtractor
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<DocumentExtractorService> _logger;

    public DocumentExtractorService(
        IHttpClientFactory httpClientFactory,
        ILogger<DocumentExtractorService> logger)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<ExtractionResult> ExtractFromPdfAsync(
        Stream pdfStream,
        string fileName,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // PdfPig requires a seekable stream — copy to memory if needed
            using var memoryStream = new MemoryStream();
            await pdfStream.CopyToAsync(memoryStream, cancellationToken);
            memoryStream.Position = 0;

            using var document = PdfDocument.Open(memoryStream);
            var textBuilder = new StringBuilder();

            _logger.LogInformation("Extracting text from PDF '{FileName}' with {PageCount} pages",
                fileName, document.NumberOfPages);

            foreach (var page in document.GetPages())
            {
                cancellationToken.ThrowIfCancellationRequested();

                var pageText = page.Text;
                if (!string.IsNullOrWhiteSpace(pageText))
                {
                    textBuilder.AppendLine(pageText);
                }

                textBuilder.AppendLine();
            }

            var extractedText = textBuilder.ToString().Trim();

            if (extractedText.Length < 50)
            {
                throw new InvalidOperationException(
                    "Could not extract readable text from the PDF. The file may be scanned or image-based.");
            }

            var title = DeriveTitleFromFileName(fileName);

            _logger.LogInformation(
                "Successfully extracted {CharCount} characters from PDF '{FileName}'",
                extractedText.Length, fileName);

            return new ExtractionResult
            {
                Content = extractedText,
                Title = title,
                CharacterCount = extractedText.Length
            };
        }
        catch (InvalidOperationException)
        {
            throw;
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("PDF extraction was cancelled for '{FileName}'", fileName);
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to extract text from PDF '{FileName}'", fileName);
            throw new InvalidOperationException(
                "Could not extract readable text from the PDF. The file may be scanned or image-based.", ex);
        }
    }

    /// <inheritdoc />
    public async Task<ExtractionResult> ExtractFromDocxAsync(
        Stream docxStream,
        string fileName,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // OpenXml requires a seekable stream — copy to memory if needed
            using var memoryStream = new MemoryStream();
            await docxStream.CopyToAsync(memoryStream, cancellationToken);
            memoryStream.Position = 0;

            using var document = WordprocessingDocument.Open(memoryStream, false);
            var body = document.MainDocumentPart?.Document?.Body;

            if (body is null)
            {
                throw new InvalidOperationException(
                    "Could not extract readable text from the Word document.");
            }

            cancellationToken.ThrowIfCancellationRequested();

            var textBuilder = new StringBuilder();
            var paragraphs = body.Descendants<DocumentFormat.OpenXml.Wordprocessing.Paragraph>();

            foreach (var paragraph in paragraphs)
            {
                var text = paragraph.InnerText;
                if (!string.IsNullOrWhiteSpace(text))
                {
                    textBuilder.AppendLine(text);
                }
                else
                {
                    // Preserve paragraph breaks
                    textBuilder.AppendLine();
                }
            }

            var extractedText = textBuilder.ToString().Trim();

            _logger.LogInformation("Extracting text from DOCX '{FileName}'", fileName);

            if (extractedText.Length < 50)
            {
                throw new InvalidOperationException(
                    "Could not extract readable text from the Word document.");
            }

            var title = DeriveTitleFromFileName(fileName);

            _logger.LogInformation(
                "Successfully extracted {CharCount} characters from DOCX '{FileName}'",
                extractedText.Length, fileName);

            return new ExtractionResult
            {
                Content = extractedText,
                Title = title,
                CharacterCount = extractedText.Length
            };
        }
        catch (InvalidOperationException)
        {
            throw;
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("DOCX extraction was cancelled for '{FileName}'", fileName);
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to extract text from DOCX '{FileName}'", fileName);
            throw new InvalidOperationException(
                "Could not extract readable text from the Word document.", ex);
        }
    }

    /// <inheritdoc />
    public async Task<ExtractionResult> ExtractFromUrlAsync(
        string url,
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Fetching content from URL: {Url}", url);

            var httpClient = _httpClientFactory.CreateClient("LessonParser");
            var html = await httpClient.GetStringAsync(url, cancellationToken);

            var htmlDoc = new HtmlDocument();
            htmlDoc.LoadHtml(html);

            // Remove script, style, nav, footer, header nodes before text extraction
            var nodesToRemove = htmlDoc.DocumentNode
                .SelectNodes("//script|//style|//nav|//footer|//header");
            if (nodesToRemove is not null)
            {
                foreach (var node in nodesToRemove)
                {
                    node.Remove();
                }
            }

            // Extract title from <title> tag
            var titleNode = htmlDoc.DocumentNode.SelectSingleNode("//title");
            var title = titleNode?.InnerText?.Trim();

            if (string.IsNullOrWhiteSpace(title))
            {
                // Fall back to URL hostname
                if (Uri.TryCreate(url, UriKind.Absolute, out var uri))
                {
                    title = uri.Host;
                }
                else
                {
                    title = url;
                }
            }
            else
            {
                title = HtmlEntity.DeEntitize(title);
            }

            // Extract text from remaining nodes
            var rawText = htmlDoc.DocumentNode.InnerText;
            rawText = HtmlEntity.DeEntitize(rawText);

            // Strip excess whitespace and blank lines
            var extractedText = CleanExtractedText(rawText);

            if (extractedText.Length < 100)
            {
                throw new InvalidOperationException(
                    "Could not extract meaningful content from the URL. " +
                    "The page may require authentication or be mostly image-based.");
            }

            _logger.LogInformation(
                "Successfully extracted {CharCount} characters from URL: {Url}",
                extractedText.Length, url);

            return new ExtractionResult
            {
                Content = extractedText,
                Title = title,
                CharacterCount = extractedText.Length
            };
        }
        catch (InvalidOperationException)
        {
            throw;
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("URL extraction was cancelled for '{Url}'", url);
            throw;
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "HTTP error while fetching URL: {Url}", url);
            throw new InvalidOperationException(
                "Could not extract meaningful content from the URL. " +
                "The page may require authentication or be mostly image-based.", ex);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to extract content from URL: {Url}", url);
            throw new InvalidOperationException(
                "Could not extract meaningful content from the URL. " +
                "The page may require authentication or be mostly image-based.", ex);
        }
    }

    /// <inheritdoc />
    public Task<ExtractionResult> ExtractFromTextAsync(
        string content,
        string title,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(content) || content.Trim().Length < 100)
        {
            throw new InvalidOperationException(
                "Please provide at least 100 characters of text content.");
        }

        var trimmedContent = content.Trim();

        return Task.FromResult(new ExtractionResult
        {
            Content = trimmedContent,
            Title = title,
            CharacterCount = trimmedContent.Length
        });
    }

    /// <summary>
    /// Derives a human-readable title from a filename by stripping the extension,
    /// replacing hyphens/underscores with spaces, and applying title case.
    /// </summary>
    private static string DeriveTitleFromFileName(string fileName)
    {
        var nameWithoutExtension = Path.GetFileNameWithoutExtension(fileName);
        var spaced = nameWithoutExtension.Replace('-', ' ').Replace('_', ' ');
        // Collapse multiple spaces
        spaced = Regex.Replace(spaced, @"\s+", " ").Trim();
        return CultureInfo.CurrentCulture.TextInfo.ToTitleCase(spaced.ToLowerInvariant());
    }

    /// <summary>
    /// Cleans extracted HTML text by collapsing whitespace and removing excessive blank lines.
    /// </summary>
    private static string CleanExtractedText(string rawText)
    {
        // Replace tabs and multiple spaces with single space
        var cleaned = Regex.Replace(rawText, @"[^\S\n]+", " ");
        // Replace 3+ consecutive newlines with double newline
        cleaned = Regex.Replace(cleaned, @"\n{3,}", "\n\n");
        // Trim each line
        var lines = cleaned.Split('\n')
            .Select(l => l.Trim())
            .Where(l => l.Length > 0);
        return string.Join("\n", lines).Trim();
    }
}
