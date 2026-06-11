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
/// Fake IPdfExtractionService that returns deterministic extracted text without fetching real PDFs.
/// </summary>
public class FakePdfExtractionService : IPdfExtractionService
{
    public string NextExtractedText { get; set; } = "This is extracted PDF text for testing.";
    public bool ShouldFail { get; set; } = false;

    public Task<PdfExtractionResult> ExtractTextAsync(Stream pdfStream, CancellationToken cancellationToken = default)
    {
        if (ShouldFail)
            return Task.FromResult(PdfExtractionResult.FailureResult("Fake extraction failure"));

        return Task.FromResult(PdfExtractionResult.SuccessResult(NextExtractedText, 3));
    }

    public Task<PdfExtractionResult> ExtractTextFromUrlAsync(string pdfUrl, CancellationToken cancellationToken = default)
    {
        if (ShouldFail)
            return Task.FromResult(PdfExtractionResult.FailureResult("Fake extraction failure"));

        return Task.FromResult(PdfExtractionResult.SuccessResult(NextExtractedText, 3));
    }
}
