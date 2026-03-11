using Hangfire;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QuantumBuild.Core.Application.Interfaces;
using QuantumBuild.Core.Application.Models;
using QuantumBuild.Modules.ToolboxTalks.Application.Abstractions.Storage;
using QuantumBuild.Modules.ToolboxTalks.Application.Common.Interfaces;
using QuantumBuild.Modules.ToolboxTalks.Application.DTOs.Validation;
using QuantumBuild.Modules.ToolboxTalks.Domain.Entities;
using QuantumBuild.Modules.ToolboxTalks.Domain.Enums;
using QuantumBuild.Modules.ToolboxTalks.Infrastructure.Jobs;

namespace QuantumBuild.API.Controllers;

/// <summary>
/// Controller for managing translation validation runs for toolbox talks
/// </summary>
[ApiController]
[Route("api/toolbox-talks/{talkId:guid}/validation")]
[Authorize(Policy = "Learnings.View")]
public class TranslationValidationController : ControllerBase
{
    private readonly IToolboxTalksDbContext _dbContext;
    private readonly ICurrentUserService _currentUserService;
    private readonly IR2StorageService _r2StorageService;
    private readonly ILogger<TranslationValidationController> _logger;

    public TranslationValidationController(
        IToolboxTalksDbContext dbContext,
        ICurrentUserService currentUserService,
        IR2StorageService r2StorageService,
        ILogger<TranslationValidationController> logger)
    {
        _dbContext = dbContext;
        _currentUserService = currentUserService;
        _r2StorageService = r2StorageService;
        _logger = logger;
    }

    /// <summary>
    /// Start a new translation validation run for a toolbox talk
    /// </summary>
    [HttpPost("validate")]
    [Authorize(Policy = "Learnings.Admin")]
    [ProducesResponseType(typeof(StartValidationResponse), StatusCodes.Status202Accepted)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> StartValidation(
        Guid talkId,
        [FromBody] StartValidationRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var tenantId = _currentUserService.TenantId;

            var talkExists = await _dbContext.ToolboxTalks
                .AnyAsync(t => t.Id == talkId && t.TenantId == tenantId, cancellationToken);

            if (!talkExists)
                return NotFound(new { message = "Toolbox talk not found" });

            if (string.IsNullOrWhiteSpace(request.LanguageCode))
                return BadRequest(new { message = "Language code is required" });

            // Check that a translation exists for this language
            var translationExists = await _dbContext.ToolboxTalkTranslations
                .AnyAsync(t => t.ToolboxTalkId == talkId && t.LanguageCode == request.LanguageCode,
                    cancellationToken);

            if (!translationExists)
                return BadRequest(new { message = $"No translation found for language '{request.LanguageCode}'" });

            var run = new TranslationValidationRun
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                ToolboxTalkId = talkId,
                LanguageCode = request.LanguageCode,
                SectorKey = request.SectorKey,
                PassThreshold = request.PassThreshold ?? 75,
                SourceLanguage = request.SourceLanguage,
                ReviewerName = request.ReviewerName,
                ReviewerOrg = request.ReviewerOrg,
                ReviewerRole = request.ReviewerRole,
                DocumentRef = request.DocumentRef,
                ClientName = request.ClientName,
                AuditPurpose = request.AuditPurpose,
                Status = ValidationRunStatus.Pending
            };

            _dbContext.TranslationValidationRuns.Add(run);
            await _dbContext.SaveChangesAsync(cancellationToken);

            var jobId = BackgroundJob.Enqueue<TranslationValidationJob>(
                job => job.ExecuteAsync(run.Id, tenantId, CancellationToken.None));

            _logger.LogInformation(
                "Translation validation started for talk {TalkId}, language {Language}, runId {RunId}, jobId {JobId}",
                talkId, request.LanguageCode, run.Id, jobId);

            return Accepted(new StartValidationResponse
            {
                RunId = run.Id,
                JobId = jobId,
                Message = "Validation started"
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error starting validation for talk {TalkId}", talkId);
            return StatusCode(500, Result.Fail("Error starting validation"));
        }
    }

    /// <summary>
    /// List validation runs for a toolbox talk, paginated, newest first
    /// </summary>
    [HttpGet("runs")]
    [ProducesResponseType(typeof(PaginatedList<ValidationRunListDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetRuns(
        Guid talkId,
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 10,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var tenantId = _currentUserService.TenantId;

            var talkExists = await _dbContext.ToolboxTalks
                .AnyAsync(t => t.Id == talkId && t.TenantId == tenantId, cancellationToken);

            if (!talkExists)
                return NotFound(new { message = "Toolbox talk not found" });

            var query = _dbContext.TranslationValidationRuns
                .Where(r => r.ToolboxTalkId == talkId && r.TenantId == tenantId)
                .OrderByDescending(r => r.CreatedAt)
                .Select(r => new ValidationRunListDto
                {
                    Id = r.Id,
                    LanguageCode = r.LanguageCode,
                    SectorKey = r.SectorKey,
                    OverallScore = r.OverallScore,
                    OverallOutcome = r.OverallOutcome,
                    SafetyVerdict = r.SafetyVerdict,
                    Status = r.Status,
                    TotalSections = r.TotalSections,
                    PassedSections = r.PassedSections,
                    ReviewSections = r.ReviewSections,
                    FailedSections = r.FailedSections,
                    PassThreshold = r.PassThreshold,
                    AuditReportUrl = r.AuditReportUrl,
                    StartedAt = r.StartedAt,
                    CompletedAt = r.CompletedAt,
                    CreatedAt = r.CreatedAt
                });

            var result = await PaginatedList<ValidationRunListDto>.CreateAsync(
                query, pageNumber, pageSize);

            return Ok(Result.Ok(result));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving validation runs for talk {TalkId}", talkId);
            return StatusCode(500, Result.Fail("Error retrieving validation runs"));
        }
    }

    /// <summary>
    /// Get a single validation run with all its results
    /// </summary>
    [HttpGet("runs/{runId:guid}")]
    [ProducesResponseType(typeof(ValidationRunDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetRunById(
        Guid talkId,
        Guid runId,
        CancellationToken cancellationToken)
    {
        try
        {
            var tenantId = _currentUserService.TenantId;

            var run = await _dbContext.TranslationValidationRuns
                .Include(r => r.Results.OrderBy(res => res.SectionIndex))
                .FirstOrDefaultAsync(r => r.Id == runId
                    && r.ToolboxTalkId == talkId
                    && r.TenantId == tenantId, cancellationToken);

            if (run == null)
                return NotFound(new { message = "Validation run not found" });

            var dto = MapToDetailDto(run);
            return Ok(dto);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving validation run {RunId}", runId);
            return StatusCode(500, new { message = "Error retrieving validation run" });
        }
    }

    /// <summary>
    /// Accept a section's translation
    /// </summary>
    [HttpPut("runs/{runId:guid}/sections/{sectionIndex:int}/accept")]
    [Authorize(Policy = "Learnings.Admin")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> AcceptSection(
        Guid talkId,
        Guid runId,
        int sectionIndex,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await GetValidationResultAsync(talkId, runId, sectionIndex, cancellationToken);
            if (result == null)
                return NotFound(new { message = "Validation result not found" });

            result.ReviewerDecision = ReviewerDecision.Accepted;
            result.DecisionAt = DateTime.UtcNow;
            result.DecisionBy = _currentUserService.UserName;

            await _dbContext.SaveChangesAsync(cancellationToken);

            return Ok(new { message = "Section accepted" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error accepting section {SectionIndex} for run {RunId}", sectionIndex, runId);
            return StatusCode(500, new { message = "Error accepting section" });
        }
    }

    /// <summary>
    /// Reject a section's translation
    /// </summary>
    [HttpPut("runs/{runId:guid}/sections/{sectionIndex:int}/reject")]
    [Authorize(Policy = "Learnings.Admin")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> RejectSection(
        Guid talkId,
        Guid runId,
        int sectionIndex,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await GetValidationResultAsync(talkId, runId, sectionIndex, cancellationToken);
            if (result == null)
                return NotFound(new { message = "Validation result not found" });

            result.ReviewerDecision = ReviewerDecision.Rejected;
            result.DecisionAt = DateTime.UtcNow;
            result.DecisionBy = _currentUserService.UserName;

            await _dbContext.SaveChangesAsync(cancellationToken);

            return Ok(new { message = "Section rejected" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error rejecting section {SectionIndex} for run {RunId}", sectionIndex, runId);
            return StatusCode(500, new { message = "Error rejecting section" });
        }
    }

    /// <summary>
    /// Edit a section's translation and re-validate
    /// </summary>
    [HttpPut("runs/{runId:guid}/sections/{sectionIndex:int}/edit")]
    [Authorize(Policy = "Learnings.Admin")]
    [ProducesResponseType(StatusCodes.Status202Accepted)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> EditSection(
        Guid talkId,
        Guid runId,
        int sectionIndex,
        [FromBody] EditTranslationRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(request.EditedTranslation))
                return BadRequest(new { message = "Edited translation is required" });

            var result = await GetValidationResultAsync(talkId, runId, sectionIndex, cancellationToken);
            if (result == null)
                return NotFound(new { message = "Validation result not found" });

            // Record implicit rejection for audit trail if not already accepted
            if (result.ReviewerDecision != ReviewerDecision.Accepted)
            {
                result.ReviewerDecision = ReviewerDecision.Rejected;
                result.DecisionAt = DateTime.UtcNow;
                result.DecisionBy = _currentUserService.UserName;
                await _dbContext.SaveChangesAsync(cancellationToken);
            }

            result.ReviewerDecision = ReviewerDecision.Edited;
            result.EditedTranslation = request.EditedTranslation;
            result.DecisionAt = DateTime.UtcNow;
            result.DecisionBy = _currentUserService.UserName;

            await _dbContext.SaveChangesAsync(cancellationToken);

            // Re-validate the edited section
            var run = await _dbContext.TranslationValidationRuns
                .FirstAsync(r => r.Id == runId, cancellationToken);

            var jobId = BackgroundJob.Enqueue<TranslationValidationJob>(
                job => job.ExecuteAsync(runId, _currentUserService.TenantId, CancellationToken.None));

            _logger.LogInformation(
                "Re-validation enqueued for run {RunId} section {SectionIndex}, jobId {JobId}",
                runId, sectionIndex, jobId);

            return Accepted(new { message = "Section edited and re-validation enqueued", jobId });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error editing section {SectionIndex} for run {RunId}", sectionIndex, runId);
            return StatusCode(500, new { message = "Error editing section" });
        }
    }

    /// <summary>
    /// Retry validation for a specific section
    /// </summary>
    [HttpPost("runs/{runId:guid}/sections/{sectionIndex:int}/retry")]
    [Authorize(Policy = "Learnings.Admin")]
    [ProducesResponseType(StatusCodes.Status202Accepted)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> RetrySection(
        Guid talkId,
        Guid runId,
        int sectionIndex,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await GetValidationResultAsync(talkId, runId, sectionIndex, cancellationToken);
            if (result == null)
                return NotFound(new { message = "Validation result not found" });

            // Record implicit rejection for audit trail if not already accepted
            if (result.ReviewerDecision != ReviewerDecision.Accepted)
            {
                result.ReviewerDecision = ReviewerDecision.Rejected;
                result.DecisionAt = DateTime.UtcNow;
                result.DecisionBy = _currentUserService.UserName;
                await _dbContext.SaveChangesAsync(cancellationToken);
            }

            // Reset to Pending while retry runs
            result.ReviewerDecision = ReviewerDecision.Pending;
            result.DecisionAt = null;
            result.DecisionBy = null;
            await _dbContext.SaveChangesAsync(cancellationToken);

            var jobId = BackgroundJob.Enqueue<TranslationValidationJob>(
                job => job.ExecuteAsync(runId, _currentUserService.TenantId, CancellationToken.None));

            _logger.LogInformation(
                "Retry enqueued for run {RunId} section {SectionIndex}, jobId {JobId}",
                runId, sectionIndex, jobId);

            return Accepted(new { message = "Re-validation enqueued", jobId });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrying section {SectionIndex} for run {RunId}", sectionIndex, runId);
            return StatusCode(500, new { message = "Error retrying section" });
        }
    }

    /// <summary>
    /// Download the audit report PDF for a validation run
    /// </summary>
    [HttpGet("runs/{runId:guid}/report")]
    [ProducesResponseType(typeof(FileContentResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetReport(
        Guid talkId,
        Guid runId,
        CancellationToken cancellationToken)
    {
        try
        {
            var tenantId = _currentUserService.TenantId;

            var run = await _dbContext.TranslationValidationRuns
                .FirstOrDefaultAsync(r => r.Id == runId
                    && r.ToolboxTalkId == talkId
                    && r.TenantId == tenantId, cancellationToken);

            if (run == null)
                return NotFound(new { message = "Validation run not found" });

            if (string.IsNullOrEmpty(run.AuditReportUrl))
                return NotFound(new { message = "Audit report has not been generated yet" });

            // Download the PDF from R2 using the known key pattern
            var storagePath = $"{tenantId}/validation-reports/{runId}.pdf";
            var fileBytes = await _r2StorageService.DownloadFileAsync(storagePath, cancellationToken);

            if (fileBytes == null)
                return NotFound(new { message = "Report file not found in storage" });

            var fileName = $"ValidationReport-{run.DocumentRef ?? runId.ToString("N")[..8]}.pdf";
            return File(fileBytes, "application/pdf", fileName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving report for run {RunId}", runId);
            return StatusCode(500, new { message = "Error retrieving report" });
        }
    }

    /// <summary>
    /// Enqueue generation of the audit report PDF
    /// </summary>
    [HttpPost("runs/{runId:guid}/report/generate")]
    [Authorize(Policy = "Learnings.Admin")]
    [ProducesResponseType(StatusCodes.Status202Accepted)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GenerateReport(
        Guid talkId,
        Guid runId,
        CancellationToken cancellationToken)
    {
        try
        {
            var tenantId = _currentUserService.TenantId;

            var run = await _dbContext.TranslationValidationRuns
                .FirstOrDefaultAsync(r => r.Id == runId
                    && r.ToolboxTalkId == talkId
                    && r.TenantId == tenantId, cancellationToken);

            if (run == null)
                return NotFound(new { message = "Validation run not found" });

            var jobId = BackgroundJob.Enqueue<ValidationReportJob>(
                job => job.ExecuteAsync(runId, tenantId, CancellationToken.None));

            _logger.LogInformation(
                "Report generation enqueued for run {RunId}, jobId {JobId}",
                runId, jobId);

            return Accepted(new { message = "Report generation enqueued", jobId });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating report for run {RunId}", runId);
            return StatusCode(500, new { message = "Error generating report" });
        }
    }

    /// <summary>
    /// Soft delete a validation run
    /// </summary>
    [HttpDelete("runs/{runId:guid}")]
    [Authorize(Policy = "Learnings.Admin")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteRun(
        Guid talkId,
        Guid runId,
        CancellationToken cancellationToken)
    {
        try
        {
            var tenantId = _currentUserService.TenantId;

            var run = await _dbContext.TranslationValidationRuns
                .FirstOrDefaultAsync(r => r.Id == runId
                    && r.ToolboxTalkId == talkId
                    && r.TenantId == tenantId, cancellationToken);

            if (run == null)
                return NotFound(new { message = "Validation run not found" });

            run.IsDeleted = true;
            await _dbContext.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Validation run {RunId} soft-deleted", runId);

            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting validation run {RunId}", runId);
            return StatusCode(500, new { message = "Error deleting validation run" });
        }
    }

    #region Private Helpers

    private async Task<TranslationValidationResult?> GetValidationResultAsync(
        Guid talkId, Guid runId, int sectionIndex, CancellationToken cancellationToken)
    {
        var tenantId = _currentUserService.TenantId;

        return await _dbContext.TranslationValidationResults
            .Include(r => r.ValidationRun)
            .FirstOrDefaultAsync(r => r.ValidationRunId == runId
                && r.SectionIndex == sectionIndex
                && r.ValidationRun.ToolboxTalkId == talkId
                && r.ValidationRun.TenantId == tenantId,
                cancellationToken);
    }

    private static ValidationRunDetailDto MapToDetailDto(TranslationValidationRun run)
    {
        return new ValidationRunDetailDto
        {
            Id = run.Id,
            ToolboxTalkId = run.ToolboxTalkId,
            CourseId = run.CourseId,
            LanguageCode = run.LanguageCode,
            SectorKey = run.SectorKey,
            SourceLanguage = run.SourceLanguage,
            SourceDialect = run.SourceDialect,
            PassThreshold = run.PassThreshold,
            OverallScore = run.OverallScore,
            OverallOutcome = run.OverallOutcome,
            SafetyVerdict = run.SafetyVerdict,
            TotalSections = run.TotalSections,
            PassedSections = run.PassedSections,
            ReviewSections = run.ReviewSections,
            FailedSections = run.FailedSections,
            Status = run.Status,
            AuditReportUrl = run.AuditReportUrl,
            ReviewerName = run.ReviewerName,
            ReviewerOrg = run.ReviewerOrg,
            ReviewerRole = run.ReviewerRole,
            DocumentRef = run.DocumentRef,
            ClientName = run.ClientName,
            AuditPurpose = run.AuditPurpose,
            StartedAt = run.StartedAt,
            CompletedAt = run.CompletedAt,
            CreatedAt = run.CreatedAt,
            Results = run.Results.Select(r => new ValidationResultDto
            {
                Id = r.Id,
                SectionIndex = r.SectionIndex,
                SectionTitle = r.SectionTitle,
                OriginalText = r.OriginalText,
                TranslatedText = r.TranslatedText,
                BackTranslationA = r.BackTranslationA,
                BackTranslationB = r.BackTranslationB,
                BackTranslationC = r.BackTranslationC,
                BackTranslationD = r.BackTranslationD,
                ScoreA = r.ScoreA,
                ScoreB = r.ScoreB,
                ScoreC = r.ScoreC,
                ScoreD = r.ScoreD,
                FinalScore = r.FinalScore,
                RoundsUsed = r.RoundsUsed,
                Outcome = r.Outcome,
                EngineOutcome = r.EngineOutcome,
                IsSafetyCritical = r.IsSafetyCritical,
                CriticalTerms = r.CriticalTerms,
                GlossaryMismatches = r.GlossaryMismatches,
                EffectiveThreshold = r.EffectiveThreshold,
                ReviewerDecision = r.ReviewerDecision,
                EditedTranslation = r.EditedTranslation,
                DecisionAt = r.DecisionAt,
                DecisionBy = r.DecisionBy
            }).ToList()
        };
    }

    #endregion
}
