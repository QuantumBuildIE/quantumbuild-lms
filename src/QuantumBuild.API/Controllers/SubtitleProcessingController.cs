using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QuantumBuild.Core.Application.Interfaces;
using QuantumBuild.Modules.ToolboxTalks.Application.DTOs.Subtitles;
using QuantumBuild.Modules.ToolboxTalks.Application.Services.Subtitles;

namespace QuantumBuild.API.Controllers;

/// <summary>
/// Controller for managing subtitle processing for toolbox talk videos
/// </summary>
[ApiController]
[Route("api/toolbox-talks/{toolboxTalkId:guid}/subtitles")]
[Authorize]
public class SubtitleProcessingController : ControllerBase
{
    private readonly ISubtitleProcessingOrchestrator _orchestrator;
    private readonly ILanguageCodeService _languageCodeService;
    private readonly ICoreDbContext _coreDbContext;
    private readonly ILogger<SubtitleProcessingController> _logger;

    public SubtitleProcessingController(
        ISubtitleProcessingOrchestrator orchestrator,
        ILanguageCodeService languageCodeService,
        ICoreDbContext coreDbContext,
        ILogger<SubtitleProcessingController> logger)
    {
        _orchestrator = orchestrator;
        _languageCodeService = languageCodeService;
        _coreDbContext = coreDbContext;
        _logger = logger;
    }

    /// <summary>
    /// Start subtitle processing for a toolbox talk
    /// </summary>
    /// <param name="toolboxTalkId">The toolbox talk ID</param>
    /// <param name="request">Processing request with video URL and target languages</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Job ID and status URL</returns>
    [HttpPost("process")]
    [Authorize(Policy = "Learnings.Manage")]
    [ProducesResponseType(typeof(StartProcessingResponse), StatusCodes.Status202Accepted)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> StartProcessing(
        Guid toolboxTalkId,
        [FromBody] StartSubtitleProcessingRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            // Validate languages
            var invalidLanguages = new List<string>();
            foreach (var l in request.TargetLanguages)
            {
                if (!await _languageCodeService.IsValidLanguageAsync(l))
                    invalidLanguages.Add(l);
            }

            if (invalidLanguages.Count != 0)
            {
                var allLanguages = await _languageCodeService.GetAllLanguagesAsync();
                return BadRequest(new
                {
                    Error = "Invalid languages",
                    InvalidLanguages = invalidLanguages,
                    ValidLanguages = allLanguages.Keys
                });
            }

            var jobId = await _orchestrator.StartProcessingAsync(
                toolboxTalkId,
                request.VideoUrl,
                request.VideoSourceType,
                request.TargetLanguages,
                cancellationToken);

            return Accepted(new StartProcessingResponse
            {
                JobId = jobId,
                Message = "Processing started",
                StatusUrl = $"/api/toolbox-talks/{toolboxTalkId}/subtitles/status"
            });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { Error = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to start subtitle processing for {TalkId}", toolboxTalkId);
            return StatusCode(500, new { Error = "Failed to start processing" });
        }
    }

    /// <summary>
    /// Get the current processing status
    /// </summary>
    /// <param name="toolboxTalkId">The toolbox talk ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Current processing status</returns>
    [HttpGet("status")]
    [Authorize(Policy = "Learnings.View")]
    [ProducesResponseType(typeof(SubtitleProcessingStatusDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetStatus(Guid toolboxTalkId, CancellationToken cancellationToken)
    {
        var status = await _orchestrator.GetStatusAsync(toolboxTalkId, cancellationToken);

        if (status == null)
            return NotFound(new { Error = "No processing job found for this learning" });

        return Ok(status);
    }

    /// <summary>
    /// Cancel an active subtitle processing job
    /// </summary>
    /// <param name="toolboxTalkId">The toolbox talk ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Success message</returns>
    [HttpPost("cancel")]
    [Authorize(Policy = "Learnings.Manage")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> CancelProcessing(Guid toolboxTalkId, CancellationToken cancellationToken)
    {
        try
        {
            var cancelled = await _orchestrator.CancelProcessingAsync(toolboxTalkId, cancellationToken);

            if (!cancelled)
                return NotFound(new { Error = "No active processing job found for this learning" });

            return Ok(new { Message = "Processing cancelled successfully" });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { Error = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to cancel subtitle processing for {TalkId}", toolboxTalkId);
            return StatusCode(500, new { Error = "Failed to cancel processing" });
        }
    }

    /// <summary>
    /// Retry failed translations for an existing job
    /// </summary>
    /// <param name="toolboxTalkId">The toolbox talk ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Job ID and status URL</returns>
    [HttpPost("retry")]
    [Authorize(Policy = "Learnings.Manage")]
    [ProducesResponseType(typeof(StartProcessingResponse), StatusCodes.Status202Accepted)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> RetryFailedTranslations(Guid toolboxTalkId, CancellationToken cancellationToken)
    {
        try
        {
            var jobId = await _orchestrator.RetryFailedTranslationsAsync(toolboxTalkId, cancellationToken);

            if (jobId == null)
                return BadRequest(new { Error = "No failed translations to retry" });

            return Accepted(new StartProcessingResponse
            {
                JobId = jobId.Value,
                Message = "Retry started",
                StatusUrl = $"/api/toolbox-talks/{toolboxTalkId}/subtitles/status"
            });
        }
        catch (InvalidOperationException ex)
        {
            if (ex.Message.Contains("No processing job found"))
                return NotFound(new { Error = ex.Message });
            return BadRequest(new { Error = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to retry subtitle processing for {TalkId}", toolboxTalkId);
            return StatusCode(500, new { Error = "Failed to retry processing" });
        }
    }

    /// <summary>
    /// Download subtitle file for a specific language
    /// </summary>
    /// <param name="toolboxTalkId">The toolbox talk ID</param>
    /// <param name="languageCode">ISO 639-1 language code (e.g., 'en', 'es', 'fr')</param>
    /// <param name="format">Format: 'srt' (default) or 'vtt' (WebVTT for browser video players)</param>
    /// <param name="download">If true, returns as attachment for download</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Subtitle file content in requested format</returns>
    [HttpGet("{languageCode}")]
    [Authorize(Policy = "Learnings.View")]
    [ProducesResponseType(typeof(string), StatusCodes.Status200OK, "text/vtt")]
    [ProducesResponseType(typeof(string), StatusCodes.Status200OK, "application/x-subrip")]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetSubtitleFile(
        Guid toolboxTalkId,
        string languageCode,
        [FromQuery] string format = "srt",
        [FromQuery] bool download = false,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var srtContent = await _orchestrator.GetSrtContentAsync(toolboxTalkId, languageCode, cancellationToken);

            if (srtContent == null)
                return NotFound(new { Error = $"No completed subtitle found for language code '{languageCode}'" });

            string content;
            string contentType;
            string fileExtension;

            if (format.Equals("vtt", StringComparison.OrdinalIgnoreCase))
            {
                content = ConvertSrtToVtt(srtContent);
                contentType = "text/vtt; charset=utf-8";
                fileExtension = "vtt";
            }
            else
            {
                content = srtContent;
                contentType = "application/x-subrip; charset=utf-8";
                fileExtension = "srt";
            }

            var fileName = $"subtitles_{languageCode}.{fileExtension}";

            if (download)
            {
                return File(
                    System.Text.Encoding.UTF8.GetBytes(content),
                    contentType,
                    fileName);
            }

            // Return Content directly instead of Ok() to bypass JSON content negotiation
            return Content(content, contentType);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get subtitle file for {TalkId} language {Lang}", toolboxTalkId, languageCode);
            return StatusCode(500, new { Error = "Failed to retrieve subtitle file" });
        }
    }

    /// <summary>
    /// Converts SRT subtitle format to WebVTT format for browser video players.
    /// SRT and VTT are very similar, but VTT requires a header and uses period instead of comma for milliseconds.
    /// </summary>
    private static string ConvertSrtToVtt(string srtContent)
    {
        var lines = srtContent.Split('\n');
        var vttLines = new List<string> { "WEBVTT", "" };

        foreach (var line in lines)
        {
            var trimmedLine = line.TrimEnd('\r');

            // Convert timestamp format: 00:00:00,000 --> 00:00:00.000
            if (trimmedLine.Contains(" --> "))
            {
                // Replace comma with period in timestamps
                vttLines.Add(trimmedLine.Replace(',', '.'));
            }
            else
            {
                vttLines.Add(trimmedLine);
            }
        }

        return string.Join("\n", vttLines);
    }

    /// <summary>
    /// Get available languages from employees
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Languages used by employees and all supported languages</returns>
    [HttpGet("/api/subtitles/available-languages")]
    [Authorize(Policy = "Learnings.View")]
    [ProducesResponseType(typeof(AvailableLanguagesResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAvailableLanguages(CancellationToken cancellationToken)
    {
        // Get languages from employees
        var employeeLanguages = await _coreDbContext.Employees
            .Where(e => !e.IsDeleted && e.PreferredLanguage != null && e.PreferredLanguage != "")
            .GroupBy(e => e.PreferredLanguage)
            .Select(g => new EmployeeLanguageInfo
            {
                Language = g.Key!,
                LanguageCode = "", // Will be set below
                EmployeeCount = g.Count()
            })
            .ToListAsync(cancellationToken);

        // Add language codes and convert codes to names where needed
        foreach (var lang in employeeLanguages)
        {
            // Check if this is a language code or language name
            if (lang.Language.Length == 2 || lang.Language.Length == 3)
            {
                // It's likely a code, try to get the name
                var name = await _languageCodeService.GetLanguageNameAsync(lang.Language);
                if (name != lang.Language)
                {
                    lang.LanguageCode = lang.Language;
                    lang.Language = name;
                    continue;
                }
            }

            // It's a name, get the code
            lang.LanguageCode = await _languageCodeService.GetLanguageCodeAsync(lang.Language);
        }

        // Get all supported languages
        var supportedLanguages = await _languageCodeService.GetAllLanguagesAsync();
        var allLanguages = supportedLanguages
            .Select(kvp => new SupportedLanguageInfo
            {
                Language = kvp.Key,
                LanguageCode = kvp.Value
            })
            .ToList();

        return Ok(new AvailableLanguagesResponse
        {
            EmployeeLanguages = employeeLanguages,
            AllSupportedLanguages = allLanguages
        });
    }
}

/// <summary>
/// Response when starting subtitle processing
/// </summary>
public class StartProcessingResponse
{
    /// <summary>
    /// The created job ID
    /// </summary>
    public Guid JobId { get; set; }

    /// <summary>
    /// Status message
    /// </summary>
    public string Message { get; set; } = string.Empty;

    /// <summary>
    /// URL to check processing status
    /// </summary>
    public string StatusUrl { get; set; } = string.Empty;
}

/// <summary>
/// Response containing available languages
/// </summary>
public class AvailableLanguagesResponse
{
    /// <summary>
    /// Languages configured for employees with counts
    /// </summary>
    public List<EmployeeLanguageInfo> EmployeeLanguages { get; set; } = new();

    /// <summary>
    /// All languages supported by the system
    /// </summary>
    public List<SupportedLanguageInfo> AllSupportedLanguages { get; set; } = new();
}

/// <summary>
/// Language with employee count
/// </summary>
public class EmployeeLanguageInfo
{
    /// <summary>
    /// Language display name
    /// </summary>
    public string Language { get; set; } = string.Empty;

    /// <summary>
    /// ISO 639-1 language code
    /// </summary>
    public string LanguageCode { get; set; } = string.Empty;

    /// <summary>
    /// Number of employees using this language
    /// </summary>
    public int EmployeeCount { get; set; }
}

/// <summary>
/// Supported language info
/// </summary>
public class SupportedLanguageInfo
{
    /// <summary>
    /// Language display name
    /// </summary>
    public string Language { get; set; } = string.Empty;

    /// <summary>
    /// ISO 639-1 language code
    /// </summary>
    public string LanguageCode { get; set; } = string.Empty;
}
