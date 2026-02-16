namespace QuantumBuild.Modules.ToolboxTalks.Application.Services;

/// <summary>
/// Service that orchestrates the extraction of content from both video transcripts and PDFs,
/// preparing the combined content for AI generation of toolbox talk sections and quiz questions.
/// </summary>
public interface IContentExtractionService
{
    /// <summary>
    /// Extracts content from video and/or PDF based on what's available for the toolbox talk.
    /// Caches the extracted content in the ToolboxTalk entity for subsequent use.
    /// </summary>
    /// <param name="toolboxTalkId">The ID of the toolbox talk to extract content for</param>
    /// <param name="includeVideo">Whether to extract video transcript content</param>
    /// <param name="includePdf">Whether to extract PDF document content</param>
    /// <param name="tenantId">The tenant ID (required for background jobs that run outside HTTP context)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Result containing combined content and extraction details</returns>
    Task<ContentExtractionResult> ExtractContentAsync(
        Guid toolboxTalkId,
        bool includeVideo,
        bool includePdf,
        Guid tenantId,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Result of a content extraction operation.
/// </summary>
/// <param name="Success">Whether the extraction was successful (at least one source was extracted)</param>
/// <param name="PartialSuccess">True if some sources succeeded but others failed (content was still generated)</param>
/// <param name="CombinedContent">The combined content from all sources, formatted for AI processing</param>
/// <param name="VideoContent">Information about extracted video content, if any</param>
/// <param name="PdfContent">Information about extracted PDF content, if any</param>
/// <param name="Errors">List of critical errors that prevented all extraction</param>
/// <param name="Warnings">List of non-fatal warnings (e.g., one source failed but another succeeded, potential OCR issues)</param>
public record ContentExtractionResult(
    bool Success,
    bool PartialSuccess,
    string? CombinedContent,
    VideoContentInfo? VideoContent,
    PdfContentInfo? PdfContent,
    List<string> Errors,
    List<string> Warnings);

/// <summary>
/// Information about extracted video content.
/// </summary>
/// <param name="FullTranscript">The complete video transcript with timestamps</param>
/// <param name="FinalPortionTranscript">Transcript from the 80-100% portion for quiz question generation</param>
/// <param name="Duration">Total video duration</param>
/// <param name="SegmentCount">Number of transcript segments</param>
public record VideoContentInfo(
    string FullTranscript,
    string FinalPortionTranscript,
    TimeSpan Duration,
    int SegmentCount);

/// <summary>
/// Information about extracted PDF content.
/// </summary>
/// <param name="FullText">The complete text extracted from the PDF</param>
/// <param name="PageCount">Number of pages in the PDF</param>
public record PdfContentInfo(
    string FullText,
    int PageCount);
