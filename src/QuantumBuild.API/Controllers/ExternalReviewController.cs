using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using QuantumBuild.Core.Application.Models;
using QuantumBuild.Modules.ToolboxTalks.Application.Abstractions.Workflows;
using QuantumBuild.Modules.ToolboxTalks.Application.DTOs.Workflows;

namespace QuantumBuild.API.Controllers;

/// <summary>
/// Public portal endpoints for external reviewers interacting via invitation token.
/// No JWT required — authentication is token-based (raw token in URL path).
/// </summary>
[ApiController]
[Route("api/external-review")]
[AllowAnonymous]
public class ExternalReviewController(
    ITranslationWorkflowService workflowService,
    ILogger<ExternalReviewController> logger) : ControllerBase
{
    /// <summary>
    /// Loads the portal context for an external reviewer given their invitation token.
    /// Returns 200 with the DTO for Active and Used states, 410 for Revoked/Expired, 404 if the token is unknown.
    /// </summary>
    [HttpGet("{token}")]
    [ProducesResponseType(typeof(ExternalReviewPortalDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status410Gone)]
    public async Task<IActionResult> GetPortalContext(string token, CancellationToken ct)
    {
        try
        {
            var result = await workflowService.GetPortalContext(token, ct);

            if (!result.Success)
            {
                if (result.ErrorCode == FailureCode.WorkflowTokenInvalid)
                    return NotFound(new { error = result.Errors.FirstOrDefault() });
                return BadRequest(new { error = result.Errors.FirstOrDefault() });
            }

            var dto = result.Data!;

            if (dto.PortalStatus is "Revoked" or "Expired")
                return StatusCode(StatusCodes.Status410Gone, dto);

            return Ok(dto);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error loading external review portal context for token");
            return StatusCode(500, new { error = "Error loading portal context" });
        }
    }

    /// <summary>
    /// Handles an external reviewer's submission (accept or reject with optional edited content).
    /// </summary>
    [HttpPost("{token}/submit")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status410Gone)]
    public async Task<IActionResult> Submit(
        string token,
        [FromBody] SubmitExternalReviewRequest request,
        CancellationToken ct)
    {
        try
        {
            var result = await workflowService.SubmitExternalReview(token, request.Accepted, request.EditedContent, ct);

            if (!result.Success)
            {
                return result.ErrorCode switch
                {
                    FailureCode.WorkflowTokenInvalid    => NotFound(new { error = result.Errors.FirstOrDefault() }),
                    FailureCode.WorkflowTokenAlreadyUsed => Conflict(new { error = result.Errors.FirstOrDefault() }),
                    FailureCode.WorkflowTokenExpired    => StatusCode(StatusCodes.Status410Gone, new { error = result.Errors.FirstOrDefault() }),
                    FailureCode.WorkflowInvalidState    => Conflict(new { error = result.Errors.FirstOrDefault() }),
                    _                                   => BadRequest(new { error = result.Errors.FirstOrDefault() })
                };
            }

            return Ok(new { message = "Review submitted" });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error submitting external review for token");
            return StatusCode(500, new { error = "Error submitting review" });
        }
    }

    /// <summary>
    /// Records an external reviewer's explicit decline of the review invitation.
    /// A non-empty reason is mandatory.
    /// </summary>
    [HttpPost("{token}/decline")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status410Gone)]
    public async Task<IActionResult> Decline(
        string token,
        [FromBody] DeclineExternalReviewRequest request,
        CancellationToken ct)
    {
        try
        {
            var result = await workflowService.DeclineExternalReview(token, request.Reason, ct);

            if (!result.Success)
            {
                return result.ErrorCode switch
                {
                    FailureCode.WorkflowTokenInvalid  => NotFound(new { error = result.Errors.FirstOrDefault() }),
                    FailureCode.WorkflowTokenExpired  => StatusCode(StatusCodes.Status410Gone, new { error = result.Errors.FirstOrDefault() }),
                    FailureCode.WorkflowReasonRequired => BadRequest(new { error = result.Errors.FirstOrDefault() }),
                    FailureCode.WorkflowInvalidState  => Conflict(new { error = result.Errors.FirstOrDefault() }),
                    _                                 => BadRequest(new { error = result.Errors.FirstOrDefault() })
                };
            }

            return Ok(new { message = "Decline recorded" });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error recording external review decline for token");
            return StatusCode(500, new { error = "Error recording decline" });
        }
    }
}

public record SubmitExternalReviewRequest
{
    public bool Accepted { get; init; }
    public string? EditedContent { get; init; }
}

public record DeclineExternalReviewRequest
{
    public string Reason { get; init; } = string.Empty;
}
