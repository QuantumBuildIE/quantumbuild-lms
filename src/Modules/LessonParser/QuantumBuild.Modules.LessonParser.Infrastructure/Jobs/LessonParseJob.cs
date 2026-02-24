using System.Diagnostics;
using Hangfire;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using QuantumBuild.Modules.LessonParser.Application.Abstractions;
using QuantumBuild.Modules.LessonParser.Application.Common.Interfaces;
using QuantumBuild.Modules.LessonParser.Domain.Enums;
using QuantumBuild.Modules.LessonParser.Infrastructure.Hubs;

namespace QuantumBuild.Modules.LessonParser.Infrastructure.Jobs;

/// <summary>
/// Hangfire background job for orchestrating AI lesson generation from extracted content.
/// Calls the LessonGeneratorService to create ToolboxTalks and a Course,
/// and reports real-time progress via SignalR.
/// </summary>
public class LessonParseJob
{
    private readonly ILessonParserDbContext _dbContext;
    private readonly IDocumentExtractor _documentExtractor;
    private readonly ILessonGeneratorService _lessonGeneratorService;
    private readonly IHubContext<LessonParserHub> _hubContext;
    private readonly ILogger<LessonParseJob> _logger;

    public LessonParseJob(
        ILessonParserDbContext dbContext,
        IDocumentExtractor documentExtractor,
        ILessonGeneratorService lessonGeneratorService,
        IHubContext<LessonParserHub> hubContext,
        ILogger<LessonParseJob> logger)
    {
        _dbContext = dbContext;
        _documentExtractor = documentExtractor;
        _lessonGeneratorService = lessonGeneratorService;
        _hubContext = hubContext;
        _logger = logger;
    }

    /// <summary>
    /// Executes the lesson parse job.
    /// </summary>
    /// <param name="parseJobId">The ParseJob entity ID</param>
    /// <param name="extractedContent">The extracted text content from the source document</param>
    /// <param name="contentTitle">Title derived from the source document</param>
    /// <param name="connectionId">SignalR connection ID for direct client updates</param>
    [AutomaticRetry(Attempts = 1)]
    [Queue("content-generation")]
    public async Task ExecuteAsync(
        Guid parseJobId,
        string extractedContent,
        string contentTitle,
        string connectionId)
    {
        var stopwatch = Stopwatch.StartNew();

        _logger.LogInformation(
            "========== LESSON PARSE JOB STARTED ==========\n" +
            "ParseJobId: {ParseJobId}\n" +
            "ContentTitle: {ContentTitle}\n" +
            "ConnectionId: {ConnectionId}\n" +
            "ContentLength: {ContentLength} chars",
            parseJobId,
            contentTitle,
            connectionId ?? "none",
            extractedContent?.Length ?? 0);

        try
        {
            // Load ParseJob from database (bypass tenant filter since Hangfire runs outside HTTP context)
            var parseJob = await _dbContext.ParseJobs
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(j => j.Id == parseJobId && !j.IsDeleted);

            if (parseJob == null)
            {
                _logger.LogError("ParseJob {ParseJobId} not found", parseJobId);
                return;
            }

            // Set status to Processing
            parseJob.Status = ParseJobStatus.Processing;
            await _dbContext.SaveChangesAsync(CancellationToken.None);

            // Send initial progress
            await SendProgressAsync(connectionId, new LessonParseProgress
            {
                Stage = "Starting content analysis...",
                PercentComplete = 0
            });

            // Create progress reporter that sends SignalR updates
            var progress = new Progress<LessonParseProgress>(async p =>
            {
                _logger.LogInformation(
                    "[Progress] ParseJob {ParseJobId}: Stage={Stage}, Percent={Percent}%",
                    parseJobId, p.Stage, p.PercentComplete);

                await SendProgressAsync(connectionId, p);
            });

            // Build ExtractionResult from the stored content
            var extraction = new ExtractionResult
            {
                Content = extractedContent,
                Title = contentTitle,
                CharacterCount = extractedContent?.Length ?? 0
            };

            // Call the AI lesson generator
            var result = await _lessonGeneratorService.GenerateFromContentAsync(
                extraction,
                parseJob.TenantId,
                parseJob.CreatedBy,
                progress,
                CancellationToken.None);

            stopwatch.Stop();

            // Update ParseJob on success
            parseJob.Status = ParseJobStatus.Completed;
            parseJob.GeneratedCourseId = result.CourseId;
            parseJob.GeneratedCourseTitle = result.CourseTitle;
            parseJob.TalksGenerated = result.TalksGenerated;
            parseJob.ExtractedContent = null; // Clear to save storage
            await _dbContext.SaveChangesAsync(CancellationToken.None);

            // Send completed notification
            await SendCompletedAsync(connectionId, result);

            _logger.LogInformation(
                "========== LESSON PARSE JOB COMPLETED ==========\n" +
                "ParseJobId: {ParseJobId}\n" +
                "Duration: {Duration}ms ({DurationSeconds:F1}s)\n" +
                "CourseId: {CourseId}\n" +
                "CourseTitle: {CourseTitle}\n" +
                "TalksGenerated: {TalksGenerated}",
                parseJobId,
                stopwatch.ElapsedMilliseconds,
                stopwatch.ElapsedMilliseconds / 1000.0,
                result.CourseId,
                result.CourseTitle,
                result.TalksGenerated);
        }
        catch (Exception ex)
        {
            stopwatch.Stop();

            _logger.LogError(ex,
                "========== LESSON PARSE JOB FAILED ==========\n" +
                "ParseJobId: {ParseJobId}\n" +
                "Duration before error: {Duration}ms\n" +
                "Exception Type: {ExceptionType}\n" +
                "Exception Message: {ExceptionMessage}",
                parseJobId,
                stopwatch.ElapsedMilliseconds,
                ex.GetType().FullName,
                ex.Message);

            // Update ParseJob on failure — keep ExtractedContent for retry
            try
            {
                var parseJob = await _dbContext.ParseJobs
                    .IgnoreQueryFilters()
                    .FirstOrDefaultAsync(j => j.Id == parseJobId && !j.IsDeleted);

                if (parseJob != null)
                {
                    parseJob.Status = ParseJobStatus.Failed;
                    parseJob.ErrorMessage = ex.Message;
                    await _dbContext.SaveChangesAsync(CancellationToken.None);
                }
            }
            catch (Exception saveEx)
            {
                _logger.LogError(saveEx,
                    "Failed to update ParseJob {ParseJobId} status after error",
                    parseJobId);
            }

            // Send failure notification
            await SendFailedAsync(connectionId, ex.Message);

            // Do NOT rethrow — job failure is handled via ParseJob status
        }
    }

    private async Task SendProgressAsync(string? connectionId, LessonParseProgress progress)
    {
        try
        {
            if (!string.IsNullOrEmpty(connectionId))
            {
                await _hubContext.Clients.Client(connectionId)
                    .SendAsync("ReceiveProgress", progress);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to send progress update via SignalR");
        }
    }

    private async Task SendCompletedAsync(string? connectionId, LessonParseResult result)
    {
        try
        {
            if (!string.IsNullOrEmpty(connectionId))
            {
                await _hubContext.Clients.Client(connectionId)
                    .SendAsync("ReceiveCompleted", result);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to send completed notification via SignalR");
        }
    }

    private async Task SendFailedAsync(string? connectionId, string errorMessage)
    {
        try
        {
            if (!string.IsNullOrEmpty(connectionId))
            {
                await _hubContext.Clients.Client(connectionId)
                    .SendAsync("ReceiveFailed", errorMessage);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to send failure notification via SignalR");
        }
    }
}
