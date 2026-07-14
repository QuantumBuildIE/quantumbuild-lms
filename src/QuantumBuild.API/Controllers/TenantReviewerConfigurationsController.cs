using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using QuantumBuild.Core.Application.Interfaces;
using QuantumBuild.Modules.ToolboxTalks.Application.Abstractions.Reviewers;
using QuantumBuild.Modules.ToolboxTalks.Application.DTOs.Reviewers;

namespace QuantumBuild.API.Controllers;

/// <summary>
/// Per-tenant external reviewer configuration — language-specific reviewer email,
/// or an "all languages" fallback. Always scoped to the caller's own tenant.
/// </summary>
[ApiController]
[Route("api/tenant-reviewer-configurations")]
[Authorize(Policy = "Learnings.View")]
public class TenantReviewerConfigurationsController(
    ITenantReviewerConfigurationService reviewerConfigurationService,
    IValidator<CreateTenantReviewerConfigurationRequest> createValidator,
    IValidator<UpdateTenantReviewerConfigurationRequest> updateValidator,
    ICurrentUserService currentUserService) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType(typeof(List<TenantReviewerConfigurationDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll(CancellationToken cancellationToken)
    {
        var result = await reviewerConfigurationService.GetAllAsync(currentUserService.TenantId, cancellationToken);
        return Ok(result);
    }

    [HttpPost]
    [Authorize(Policy = "Learnings.Admin")]
    [ProducesResponseType(typeof(TenantReviewerConfigurationDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Create(
        [FromBody] CreateTenantReviewerConfigurationRequest request,
        CancellationToken cancellationToken)
    {
        var validationResult = await createValidator.ValidateAsync(request, cancellationToken);
        if (!validationResult.IsValid)
            return BadRequest(new { errors = validationResult.Errors.Select(e => e.ErrorMessage) });

        try
        {
            var result = await reviewerConfigurationService.CreateAsync(
                currentUserService.TenantId, request.LanguageCode, request.ReviewerEmail, request.ReviewerName, cancellationToken);

            return CreatedAtAction(nameof(GetAll), null, result);
        }
        catch (InvalidOperationException ex) when (ex.Message.StartsWith("CONFLICT:"))
        {
            return Conflict(new { error = ex.Message.Replace("CONFLICT:", "") });
        }
        catch (InvalidOperationException ex) when (ex.Message.StartsWith("VALIDATION:"))
        {
            return BadRequest(new { error = ex.Message.Replace("VALIDATION:", "") });
        }
    }

    [HttpPut("{id:guid}")]
    [Authorize(Policy = "Learnings.Admin")]
    [ProducesResponseType(typeof(TenantReviewerConfigurationDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update(
        Guid id,
        [FromBody] UpdateTenantReviewerConfigurationRequest request,
        CancellationToken cancellationToken)
    {
        var validationResult = await updateValidator.ValidateAsync(request, cancellationToken);
        if (!validationResult.IsValid)
            return BadRequest(new { errors = validationResult.Errors.Select(e => e.ErrorMessage) });

        try
        {
            var result = await reviewerConfigurationService.UpdateAsync(
                currentUserService.TenantId, id, request.ReviewerEmail, request.ReviewerName, cancellationToken);

            return Ok(result);
        }
        catch (InvalidOperationException ex)
        {
            return NotFound(new { error = ex.Message });
        }
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Policy = "Learnings.Admin")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        try
        {
            await reviewerConfigurationService.DeleteAsync(currentUserService.TenantId, id, cancellationToken);
            return NoContent();
        }
        catch (InvalidOperationException ex)
        {
            return NotFound(new { error = ex.Message });
        }
    }
}
