namespace QuantumBuild.Modules.ToolboxTalks.Application.Services;

/// <summary>
/// Service that orchestrates the full content generation flow:
/// 1. Extract content from video and/or PDF
/// 2. Generate sections using AI
/// 3. Generate quiz questions using AI
/// 4. Save generated content to database
/// </summary>
public interface IContentGenerationService
{
    /// <summary>
    /// Generates sections and quiz questions for a toolbox talk using AI.
    /// This is the main orchestration method that handles the full content generation workflow.
    /// </summary>
    /// <param name="toolboxTalkId">The ID of the toolbox talk to generate content for</param>
    /// <param name="options">Generation options (sources, minimums, thresholds)</param>
    /// <param name="tenantId">The tenant ID (required for background jobs that run outside HTTP context)</param>
    /// <param name="progress">Optional progress reporter for real-time updates</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Result containing generation statistics and any errors/warnings</returns>
    Task<ContentGenerationResult> GenerateContentAsync(
        Guid toolboxTalkId,
        ContentGenerationOptions options,
        Guid tenantId,
        IProgress<ContentGenerationProgress>? progress = null,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Options for controlling content generation behavior.
/// </summary>
/// <param name="IncludeVideo">Whether to extract and use video transcript content</param>
/// <param name="IncludePdf">Whether to extract and use PDF document content</param>
/// <param name="MinimumSections">Minimum number of sections to generate (default: 7)</param>
/// <param name="MinimumQuestions">Minimum number of quiz questions to generate (default: 5)</param>
/// <param name="PassThreshold">Quiz pass threshold percentage (default: 80)</param>
/// <param name="ReplaceExisting">True to replace existing content, false to append (default: true)</param>
/// <param name="SourceLanguageCode">The language code of the original content (default: "en")</param>
/// <param name="GenerateSlidesFromPdf">Whether to auto-generate slideshow from PDF after content generation</param>
public record ContentGenerationOptions(
    bool IncludeVideo,
    bool IncludePdf,
    int MinimumSections = 7,
    int MinimumQuestions = 5,
    int PassThreshold = 80,
    bool ReplaceExisting = true,
    string SourceLanguageCode = "en",
    bool GenerateSlidesFromPdf = false);

/// <summary>
/// Result of a content generation operation.
/// </summary>
/// <param name="Success">Whether the generation completed successfully (content was generated)</param>
/// <param name="PartialSuccess">True if some content sources failed but content was still generated from others</param>
/// <param name="SectionsGenerated">Number of sections created</param>
/// <param name="QuestionsGenerated">Number of quiz questions created</param>
/// <param name="HasFinalPortionQuestion">Whether at least one question is from video final portion</param>
/// <param name="Errors">List of critical errors that occurred</param>
/// <param name="Warnings">List of non-fatal warnings (e.g., one source failed but generation continued)</param>
/// <param name="TotalTokensUsed">Total AI tokens consumed (for cost tracking)</param>
public record ContentGenerationResult(
    bool Success,
    bool PartialSuccess,
    int SectionsGenerated,
    int QuestionsGenerated,
    bool HasFinalPortionQuestion,
    List<string> Errors,
    List<string> Warnings,
    int TotalTokensUsed);

/// <summary>
/// Progress update for content generation stages.
/// Used for real-time progress reporting via SignalR.
/// </summary>
/// <param name="Stage">Current generation stage name</param>
/// <param name="PercentComplete">Overall progress percentage (0-100)</param>
/// <param name="Message">Human-readable status message</param>
public record ContentGenerationProgress(
    string Stage,
    int PercentComplete,
    string Message);
