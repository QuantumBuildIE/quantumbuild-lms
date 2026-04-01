using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using QuantumBuild.Core.Application.Interfaces;
using QuantumBuild.Core.Application.Models;
using QuantumBuild.Modules.ToolboxTalks.Application.Abstractions.ContentCreation;

namespace QuantumBuild.API.Controllers;

/// <summary>
/// Controller for managing content creation wizard sessions
/// </summary>
[ApiController]
[Route("api/toolbox-talks/create")]
[Authorize(Policy = "Learnings.View")]
public class ContentCreationController : ControllerBase
{
    private readonly IContentCreationSessionService _sessionService;
    private readonly ICurrentUserService _currentUserService;
    private readonly ILogger<ContentCreationController> _logger;

    public ContentCreationController(
        IContentCreationSessionService sessionService,
        ICurrentUserService currentUserService,
        ILogger<ContentCreationController> logger)
    {
        _sessionService = sessionService;
        _currentUserService = currentUserService;
        _logger = logger;
    }

    /// <summary>
    /// Create a new content creation session
    /// </summary>
    [HttpPost("session")]
    [Authorize(Policy = "Learnings.Manage")]
    [ProducesResponseType(typeof(ContentCreationSessionDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CreateSession(
        [FromBody] CreateSessionRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var tenantId = _currentUserService.TenantId;
            var session = await _sessionService.CreateSessionAsync(request, tenantId, cancellationToken);
            return CreatedAtAction(nameof(GetSession), new { id = session.Id }, session);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(Result.Fail(ex.Message));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating content creation session");
            return StatusCode(500, Result.Fail("Error creating session"));
        }
    }

    /// <summary>
    /// Upload a file for a content creation session
    /// </summary>
    [HttpPost("session/{id:guid}/upload")]
    [Authorize(Policy = "Learnings.Manage")]
    [RequestSizeLimit(524288000)] // 500MB
    [ProducesResponseType(typeof(ContentCreationSessionDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UploadFile(
        Guid id,
        IFormFile file,
        CancellationToken cancellationToken)
    {
        try
        {
            var tenantId = _currentUserService.TenantId;
            var session = await _sessionService.UploadFileAsync(id, file, tenantId, cancellationToken);
            return Ok(session);
        }
        catch (InvalidOperationException ex) when (ex.Message == "Session not found")
        {
            return NotFound(new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(Result.Fail(ex.Message));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error uploading file for session {SessionId}", id);
            return StatusCode(500, Result.Fail("Error uploading file"));
        }
    }

    /// <summary>
    /// Update source content and reset session to Draft for re-parsing
    /// </summary>
    [HttpPut("session/{id:guid}/source")]
    [Authorize(Policy = "Learnings.Manage")]
    [ProducesResponseType(typeof(ContentCreationSessionDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateSource(
        Guid id,
        [FromBody] UpdateSourceRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var tenantId = _currentUserService.TenantId;
            var session = await _sessionService.UpdateSourceAsync(id, request, tenantId, cancellationToken);
            return Ok(session);
        }
        catch (InvalidOperationException ex) when (ex.Message == "Session not found")
        {
            return NotFound(new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(Result.Fail(ex.Message));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating source for session {SessionId}", id);
            return StatusCode(500, Result.Fail("Error updating source"));
        }
    }

    /// <summary>
    /// Trigger content parsing for a session
    /// </summary>
    [HttpPost("session/{id:guid}/parse")]
    [Authorize(Policy = "Learnings.Manage")]
    [ProducesResponseType(typeof(ContentCreationSessionDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ParseContent(
        Guid id,
        CancellationToken cancellationToken)
    {
        try
        {
            var tenantId = _currentUserService.TenantId;
            var session = await _sessionService.ParseContentAsync(id, tenantId, cancellationToken);
            return Ok(session);
        }
        catch (InvalidOperationException ex) when (ex.Message == "Session not found")
        {
            return NotFound(new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(Result.Fail(ex.Message));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error parsing content for session {SessionId}", id);
            return StatusCode(500, Result.Fail("Error parsing content"));
        }
    }

    /// <summary>
    /// Get current session state
    /// </summary>
    [HttpGet("session/{id:guid}")]
    [ProducesResponseType(typeof(ContentCreationSessionDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetSession(
        Guid id,
        CancellationToken cancellationToken)
    {
        try
        {
            var tenantId = _currentUserService.TenantId;
            var session = await _sessionService.GetSessionAsync(id, tenantId, cancellationToken);
            return Ok(session);
        }
        catch (InvalidOperationException ex) when (ex.Message == "Session not found")
        {
            return NotFound(new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(Result.Fail(ex.Message));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving session {SessionId}", id);
            return StatusCode(500, Result.Fail("Error retrieving session"));
        }
    }

    /// <summary>
    /// Update sections and confirm output type
    /// </summary>
    [HttpPut("session/{id:guid}/sections")]
    [Authorize(Policy = "Learnings.Manage")]
    [ProducesResponseType(typeof(ContentCreationSessionDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateSections(
        Guid id,
        [FromBody] UpdateSectionsRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var tenantId = _currentUserService.TenantId;
            var session = await _sessionService.UpdateSectionsAsync(id, request, tenantId, cancellationToken);
            return Ok(session);
        }
        catch (InvalidOperationException ex) when (ex.Message == "Session not found")
        {
            return NotFound(new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(Result.Fail(ex.Message));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating sections for session {SessionId}", id);
            return StatusCode(500, Result.Fail("Error updating sections"));
        }
    }

    /// <summary>
    /// Start translation and validation for target languages
    /// </summary>
    [HttpPost("session/{id:guid}/translate-validate")]
    [Authorize(Policy = "Learnings.Manage")]
    [ProducesResponseType(typeof(ContentCreationSessionDto), StatusCodes.Status202Accepted)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> StartTranslateValidate(
        Guid id,
        [FromBody] StartTranslateValidateRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var tenantId = _currentUserService.TenantId;
            var session = await _sessionService.StartTranslateValidateAsync(
                id, request, tenantId, cancellationToken);
            return Accepted(session);
        }
        catch (InvalidOperationException ex) when (ex.Message == "Session not found")
        {
            return NotFound(new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(Result.Fail(ex.Message));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error starting translate-validate for session {SessionId}", id);
            return StatusCode(500, Result.Fail("Error starting translate-validate"));
        }
    }

    /// <summary>
    /// Publish session as Talk or Course
    /// </summary>
    [HttpPost("session/{id:guid}/publish")]
    [Authorize(Policy = "Learnings.Manage")]
    [ProducesResponseType(typeof(PublishResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Publish(
        Guid id,
        [FromBody] PublishRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var tenantId = _currentUserService.TenantId;
            var result = await _sessionService.PublishAsync(id, request, tenantId, cancellationToken);

            if (!result.Success)
                return BadRequest(Result.Fail(result.ErrorMessage ?? "Publish failed"));

            return Ok(result);
        }
        catch (InvalidOperationException ex) when (ex.Message == "Session not found")
        {
            return NotFound(new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(Result.Fail(ex.Message));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error publishing session {SessionId}", id);
            return StatusCode(500, Result.Fail("Error publishing session"));
        }
    }

    /// <summary>
    /// Generate quiz questions from session content using AI
    /// </summary>
    [HttpPost("session/{id:guid}/generate-quiz")]
    [Authorize(Policy = "Learnings.Manage")]
    [ProducesResponseType(typeof(ContentCreationSessionDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GenerateQuiz(
        Guid id,
        CancellationToken cancellationToken)
    {
        try
        {
            var tenantId = _currentUserService.TenantId;
            var session = await _sessionService.GenerateQuizAsync(id, tenantId, cancellationToken: cancellationToken);
            return Ok(session);
        }
        catch (InvalidOperationException ex) when (ex.Message == "Session not found")
        {
            return NotFound(new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(Result.Fail(ex.Message));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating quiz for session {SessionId}", id);
            return StatusCode(500, Result.Fail("Error generating quiz"));
        }
    }

    /// <summary>
    /// Get quiz questions and settings for a session
    /// </summary>
    [HttpGet("session/{id:guid}/quiz")]
    [ProducesResponseType(typeof(SessionQuizDataDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetQuizData(
        Guid id,
        CancellationToken cancellationToken)
    {
        try
        {
            var tenantId = _currentUserService.TenantId;
            var quizData = await _sessionService.GetQuizDataAsync(id, tenantId, cancellationToken);
            return Ok(quizData);
        }
        catch (InvalidOperationException ex) when (ex.Message == "Session not found")
        {
            return NotFound(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving quiz data for session {SessionId}", id);
            return StatusCode(500, Result.Fail("Error retrieving quiz data"));
        }
    }

    /// <summary>
    /// Update quiz questions for a session
    /// </summary>
    [HttpPut("session/{id:guid}/questions")]
    [Authorize(Policy = "Learnings.Manage")]
    [ProducesResponseType(typeof(ContentCreationSessionDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateQuestions(
        Guid id,
        [FromBody] UpdateSessionQuestionsRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var tenantId = _currentUserService.TenantId;
            var session = await _sessionService.UpdateQuestionsAsync(id, request, tenantId, cancellationToken);
            return Ok(session);
        }
        catch (InvalidOperationException ex) when (ex.Message == "Session not found")
        {
            return NotFound(new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(Result.Fail(ex.Message));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating questions for session {SessionId}", id);
            return StatusCode(500, Result.Fail("Error updating questions"));
        }
    }

    /// <summary>
    /// Update quiz settings for a session
    /// </summary>
    [HttpPut("session/{id:guid}/quiz-settings")]
    [Authorize(Policy = "Learnings.Manage")]
    [ProducesResponseType(typeof(ContentCreationSessionDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateQuizSettings(
        Guid id,
        [FromBody] SessionQuizSettingsDto settings,
        CancellationToken cancellationToken)
    {
        try
        {
            var tenantId = _currentUserService.TenantId;
            var session = await _sessionService.UpdateQuizSettingsAsync(id, settings, tenantId, cancellationToken);
            return Ok(session);
        }
        catch (InvalidOperationException ex) when (ex.Message == "Session not found")
        {
            return NotFound(new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(Result.Fail(ex.Message));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating quiz settings for session {SessionId}", id);
            return StatusCode(500, Result.Fail("Error updating quiz settings"));
        }
    }

    /// <summary>
    /// Get session settings (title, category, behaviour)
    /// </summary>
    [HttpGet("session/{id:guid}/settings")]
    [ProducesResponseType(typeof(SessionSettingsDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetSettings(
        Guid id,
        CancellationToken cancellationToken)
    {
        try
        {
            var tenantId = _currentUserService.TenantId;
            var settings = await _sessionService.GetSettingsAsync(id, tenantId, cancellationToken);
            return Ok(settings);
        }
        catch (InvalidOperationException ex) when (ex.Message == "Session not found")
        {
            return NotFound(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving settings for session {SessionId}", id);
            return StatusCode(500, Result.Fail("Error retrieving settings"));
        }
    }

    /// <summary>
    /// Update session settings (title, category, behaviour)
    /// </summary>
    [HttpPut("session/{id:guid}/settings")]
    [Authorize(Policy = "Learnings.Manage")]
    [ProducesResponseType(typeof(ContentCreationSessionDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateSettings(
        Guid id,
        [FromBody] SessionSettingsDto settings,
        CancellationToken cancellationToken)
    {
        try
        {
            var tenantId = _currentUserService.TenantId;
            var session = await _sessionService.UpdateSettingsAsync(id, settings, tenantId, cancellationToken);
            return Ok(session);
        }
        catch (InvalidOperationException ex) when (ex.Message == "Session not found")
        {
            return NotFound(new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(Result.Fail(ex.Message));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating settings for session {SessionId}", id);
            return StatusCode(500, Result.Fail("Error updating settings"));
        }
    }

    /// <summary>
    /// Upload cover image for session
    /// </summary>
    [HttpPost("session/{id:guid}/cover-image")]
    [Authorize(Policy = "Learnings.Manage")]
    [RequestSizeLimit(5242880)] // 5MB
    [ProducesResponseType(typeof(ContentCreationSessionDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UploadCoverImage(
        Guid id,
        IFormFile file,
        CancellationToken cancellationToken)
    {
        try
        {
            var tenantId = _currentUserService.TenantId;
            var session = await _sessionService.UploadCoverImageAsync(id, file, tenantId, cancellationToken);
            return Ok(session);
        }
        catch (InvalidOperationException ex) when (ex.Message == "Session not found")
        {
            return NotFound(new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(Result.Fail(ex.Message));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error uploading cover image for session {SessionId}", id);
            return StatusCode(500, Result.Fail("Error uploading cover image"));
        }
    }

    /// <summary>
    /// Check if a title is available (not already used by another learning)
    /// </summary>
    [HttpGet("session/{id:guid}/check-title")]
    [ProducesResponseType(typeof(TitleCheckResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> CheckTitle(
        Guid id,
        [FromQuery] string title,
        CancellationToken cancellationToken)
    {
        try
        {
            var tenantId = _currentUserService.TenantId;
            var result = await _sessionService.CheckTitleAvailableAsync(title, id, tenantId, cancellationToken);
            return Ok(result);
        }
        catch (InvalidOperationException ex) when (ex.Message == "Session not found")
        {
            return NotFound(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking title availability for session {SessionId}", id);
            return StatusCode(500, Result.Fail("Error checking title availability"));
        }
    }

    /// <summary>
    /// Abandon and clean up a session
    /// </summary>
    [HttpDelete("session/{id:guid}")]
    [Authorize(Policy = "Learnings.Manage")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> AbandonSession(
        Guid id,
        CancellationToken cancellationToken)
    {
        try
        {
            var tenantId = _currentUserService.TenantId;
            await _sessionService.AbandonSessionAsync(id, tenantId, cancellationToken);
            return NoContent();
        }
        catch (InvalidOperationException ex) when (ex.Message == "Session not found")
        {
            return NotFound(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error abandoning session {SessionId}", id);
            return StatusCode(500, Result.Fail("Error abandoning session"));
        }
    }
}
