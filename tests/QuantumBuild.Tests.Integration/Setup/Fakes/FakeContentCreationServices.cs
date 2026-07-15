using QuantumBuild.Modules.ToolboxTalks.Application.Abstractions.ContentCreation;
using QuantumBuild.Modules.ToolboxTalks.Application.Abstractions.Pdf;
using QuantumBuild.Modules.ToolboxTalks.Domain.Enums;

namespace QuantumBuild.Tests.Integration.Setup.Fakes;

/// <summary>
/// Fake IContentParserService that returns deterministic sections without calling the Claude API.
/// </summary>
public class FakeContentParserService : IContentParserService
{
    public List<ParsedSection> NextSections { get; set; } =
    [
        new("Section 1: Introduction", "<p>Introduction content.</p>", 1),
        new("Section 2: Key Points", "<p>Key points content.</p>", 2),
    ];

    public bool ShouldFail { get; set; } = false;

    public Task<ContentParseResult> ParseContentAsync(
        string rawText, InputMode inputModeHint, Guid tenantId, Guid? userId = null,
        bool preserveSourceWording = false, CancellationToken cancellationToken = default)
    {
        if (ShouldFail)
            return Task.FromResult(new ContentParseResult(false, [], OutputType.Lesson, "Fake parse failure"));

        return Task.FromResult(new ContentParseResult(true, NextSections, SuggestOutputType(NextSections.Count)));
    }

    public OutputType SuggestOutputType(int sectionCount) =>
        sectionCount >= 3 ? OutputType.Course : OutputType.Lesson;
}

/// <summary>
/// Fake IDocxExtractionService that returns deterministic extracted text without fetching real Word documents.
/// </summary>
public class FakeDocxExtractionService : IDocxExtractionService
{
    public string NextExtractedText { get; set; } = "This is extracted Word document text for testing purposes and it is certainly longer than fifty characters.";
    public bool ShouldFail { get; set; } = false;

    public Task<DocxExtractionResult> ExtractTextFromUrlAsync(string docxUrl, CancellationToken cancellationToken = default)
    {
        if (ShouldFail)
            return Task.FromResult(new DocxExtractionResult(false, null, "Fake DOCX extraction failure"));

        return Task.FromResult(new DocxExtractionResult(true, NextExtractedText, null));
    }
}

/// <summary>
/// Fake IPdfExtractionService that returns deterministic extracted text without fetching real PDFs.
/// </summary>
public class FakePdfExtractionService : IPdfExtractionService
{
    public string NextExtractedText { get; set; } = "This is extracted PDF text for testing.";
    public bool ShouldFail { get; set; } = false;

    /// <summary>
    /// Failure category returned alongside the failure message when ShouldFail is true.
    /// Defaults to Unknown; set to one of the PdfExtractionErrorCategory constants to test a
    /// specific mapping (e.g. RequirementIngestionJob's category → job errorCode translation).
    /// </summary>
    public string NextErrorCategory { get; set; } = PdfExtractionErrorCategory.Unknown;

    public string NextErrorMessage { get; set; } = "Fake extraction failure";

    public Task<PdfExtractionResult> ExtractTextAsync(Stream pdfStream, CancellationToken cancellationToken = default)
    {
        if (ShouldFail)
            return Task.FromResult(PdfExtractionResult.FailureResult(NextErrorMessage, NextErrorCategory));

        return Task.FromResult(PdfExtractionResult.SuccessResult(NextExtractedText, 3));
    }

    public Task<PdfExtractionResult> ExtractTextFromUrlAsync(string pdfUrl, CancellationToken cancellationToken = default)
    {
        if (ShouldFail)
            return Task.FromResult(PdfExtractionResult.FailureResult(NextErrorMessage, NextErrorCategory));

        return Task.FromResult(PdfExtractionResult.SuccessResult(NextExtractedText, 3));
    }
}
